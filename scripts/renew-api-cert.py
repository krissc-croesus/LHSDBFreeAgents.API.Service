"""Renew the TLS certificate for the API's HTTPS listener.

Why this exists instead of an ACM-issued certificate: piriwin.com carries a CAA
record (`0 issue "letsencrypt.org"`) injected by the DNS host at the server
level -- it is not in the zone editor and cannot be changed from the hosting
panel. Amazon is therefore not authorised to issue for the domain: every ACM
request fails with CAA_ERROR and ACM auto-renewal is impossible. Let's Encrypt
is authorised, so we issue there and import the result into ACM.

The ACME HTTP-01 challenge is answered by the Beanstalk load balancer itself: a
temporary fixed-response rule is added to the :80 listener and removed again in
a finally block. No DNS record and no hosting-panel access are required.

Every AWS identifier is discovered at run time, so nothing environment-specific
is hard-coded here.

Usage:
    python -m venv .venv
    .venv/Scripts/pip install certbot
    ACME_EMAIL=you@example.com .venv/Scripts/python scripts/renew-api-cert.py

ACME_EMAIL is optional; setting it registers the Let's Encrypt account with an
address that receives expiry reminders.

Requires AWS credentials with acm:{ListCertificates,ImportCertificate},
elasticloadbalancing:{DescribeListeners,CreateRule,DeleteRule} and
elasticbeanstalk:DescribeEnvironmentResources.
"""
import datetime
import json
import os
import subprocess
import sys
import tempfile
import time

import josepy as jose
from acme import challenges, client, crypto_util, messages
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa

DIRECTORY_URL = "https://acme-v02.api.letsencrypt.org/directory"
DOMAIN = "lhsdb-fa-api.piriwin.com"
REGION = "us-east-2"
BEANSTALK_ENV = "Lhsdbfreeagents2020-prod-lb"


def aws(*args):
    """Run an AWS CLI command in the target region and return parsed stdout."""
    result = subprocess.run(
        ["aws", "--region", REGION] + list(args) + ["--output", "json"],
        capture_output=True, text=True, shell=True,
    )
    if result.returncode != 0:
        sys.exit("aws " + " ".join(args) + " failed:\n" + result.stderr)
    return json.loads(result.stdout) if result.stdout.strip() else None


def find_http_listener():
    """The :80 listener of the Beanstalk environment's load balancer."""
    resources = aws(
        "elasticbeanstalk", "describe-environment-resources",
        "--environment-name", BEANSTALK_ENV,
    )
    balancers = resources["EnvironmentResources"]["LoadBalancers"]
    if not balancers:
        sys.exit("no load balancer found for " + BEANSTALK_ENV)

    listeners = aws(
        "elbv2", "describe-listeners", "--load-balancer-arn", balancers[0]["Name"],
    )["Listeners"]
    for listener in listeners:
        if listener["Port"] == 80:
            return listener["ListenerArn"]
    sys.exit("no :80 listener on " + balancers[0]["Name"])


def find_imported_certificate():
    """The imported ACM certificate for DOMAIN, or None on a first run."""
    summaries = aws("acm", "list-certificates")["CertificateSummaryList"]
    matches = [
        c["CertificateArn"] for c in summaries
        if c.get("DomainName") == DOMAIN and c.get("Type") == "IMPORTED"
    ]
    if len(matches) > 1:
        sys.exit(
            "several imported certificates match " + DOMAIN + "; "
            "remove the stale ones so renewal targets a single ARN:\n  "
            + "\n  ".join(matches)
        )
    return matches[0] if matches else None


def new_rsa_key():
    return rsa.generate_private_key(public_exponent=65537, key_size=2048)


def to_pem(key):
    return key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.TraditionalOpenSSL,
        encryption_algorithm=serialization.NoEncryption(),
    )


def create_challenge_rule(listener_arn, path, body):
    conditions = [{"Field": "path-pattern", "Values": [path]}]
    actions = [{
        "Type": "fixed-response",
        "FixedResponseConfig": {
            "MessageBody": body, "StatusCode": "200", "ContentType": "text/plain",
        },
    }]
    rules = aws(
        "elbv2", "create-rule",
        "--listener-arn", listener_arn,
        "--priority", "1",
        "--conditions", json.dumps(conditions),
        "--actions", json.dumps(actions),
    )
    return rules["Rules"][0]["RuleArn"]


def issue(listener_arn):
    account_key = new_rsa_key()
    jkey = jose.JWKRSA(key=jose.ComparableRSAKey(account_key))

    net = client.ClientNetwork(jkey, user_agent="lhsdb-acme/1.0")
    acme = client.ClientV2(client.ClientV2.get_directory(DIRECTORY_URL, net), net=net)
    acme.new_account(messages.NewRegistration.from_data(
        email=os.environ.get("ACME_EMAIL"), terms_of_service_agreed=True,
    ))

    domain_key_pem = to_pem(new_rsa_key())
    order = acme.new_order(crypto_util.make_csr(domain_key_pem, [DOMAIN]))

    chall_body = next(
        (c for authz in order.authorizations for c in authz.body.challenges
         if isinstance(c.chall, challenges.HTTP01)),
        None,
    )
    if chall_body is None:
        sys.exit("no HTTP-01 challenge offered for " + DOMAIN)

    response, validation = chall_body.chall.response_and_validation(jkey)
    rule_arn = create_challenge_rule(listener_arn, chall_body.chall.path, validation)
    print("temporary challenge rule added")
    try:
        # Let the rule reach every load balancer node before Let's Encrypt
        # probes it from its validation vantage points.
        time.sleep(20)
        acme.answer_challenge(chall_body, response)
        deadline = datetime.datetime.now() + datetime.timedelta(seconds=180)
        order = acme.poll_and_finalize(order, deadline=deadline)
    finally:
        aws("elbv2", "delete-rule", "--rule-arn", rule_arn)
        print("temporary challenge rule removed")

    return domain_key_pem, order.fullchain_pem


def import_to_acm(key_pem, fullchain_pem, certificate_arn):
    blocks = fullchain_pem.split("-----END CERTIFICATE-----")
    certs = [b.strip() + "\n-----END CERTIFICATE-----\n"
             for b in blocks if "BEGIN CERTIFICATE" in b]

    workdir = tempfile.mkdtemp(prefix="acme-import-")
    files = {
        "key.pem": key_pem.decode(),
        "cert.pem": certs[0],
        "chain.pem": "".join(certs[1:]),
    }
    try:
        for name, content in files.items():
            with open(os.path.join(workdir, name), "w") as fh:
                fh.write(content)
        args = [
            "acm", "import-certificate",
            "--certificate", "fileb://" + os.path.join(workdir, "cert.pem"),
            "--private-key", "fileb://" + os.path.join(workdir, "key.pem"),
            "--certificate-chain", "fileb://" + os.path.join(workdir, "chain.pem"),
        ]
        # Re-importing onto the existing ARN leaves the HTTPS listener untouched.
        if certificate_arn:
            args += ["--certificate-arn", certificate_arn]
        return aws(*args)["CertificateArn"]
    finally:
        for name in files:
            try:
                os.remove(os.path.join(workdir, name))
            except OSError:
                pass
        os.rmdir(workdir)


def main():
    listener_arn = find_http_listener()
    existing = find_imported_certificate()

    key_pem, fullchain_pem = issue(listener_arn)
    arn = import_to_acm(key_pem, fullchain_pem, existing)

    if existing:
        print("re-imported onto the existing certificate; listener unchanged")
    else:
        print("imported as a new certificate:", arn)
        print("attach it once with:")
        print("  aws elasticbeanstalk update-environment --region " + REGION)
        print("    --environment-name " + BEANSTALK_ENV)
        print("    --option-settings Namespace=aws:elbv2:listener:443,"
              "OptionName=SSLCertificateArns,Value=" + arn)

    print("verify with:")
    print("  curl -sS -o /dev/null -w '%{http_code}\\n' https://" + DOMAIN + "/players")


if __name__ == "__main__":
    main()

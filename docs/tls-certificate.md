# TLS certificate for `lhsdb-fa-api.piriwin.com`

The API's HTTPS listener is served by a **Let's Encrypt certificate imported into
ACM**, renewed manually with `scripts/renew-api-cert.py`. It is **not** an
ACM-issued certificate and it does **not** auto-renew.

**Renew before the expiry date** — check it at any time with:

```bash
echo | openssl s_client -servername lhsdb-fa-api.piriwin.com \
  -connect lhsdb-fa-api.piriwin.com:443 2>/dev/null | openssl x509 -noout -dates
```

Certificates last 90 days. The one installed on 2026-08-06 expires **2026-11-04**.

## Why it works this way

`piriwin.com` carries a CAA record that authorises only Let's Encrypt:

```
piriwin.com.  CAA  0 issue "letsencrypt.org"
```

It is injected by the DNS host (n0c / WHC) at the server level. It does **not**
appear in the panel's zone editor and cannot be added, changed or removed from
there — only their support can touch it.

Because a CAA record applies to a domain and all its subdomains, Amazon is not
authorised to issue for `lhsdb-fa-api.piriwin.com`. Every ACM request fails with
`FailureReason: CAA_ERROR`, and ACM auto-renewal is permanently broken. This is
what took the API down on 2026-08-05, when the wildcard `*.piriwin.com`
certificate that had served it since 2024 expired without being able to renew.

Let's Encrypt *is* authorised by the CAA, hence the current arrangement.

## How renewal works

Let's Encrypt validates ownership with an HTTP-01 challenge: it fetches
`http://lhsdb-fa-api.piriwin.com/.well-known/acme-challenge/<token>`. The script
answers it **from the load balancer**, by adding a temporary fixed-response rule
on the `:80` listener and deleting it afterwards in a `finally` block.

That means renewal needs **no DNS record and no access to the hosting panel** —
only AWS credentials. It also means the `:80` listener must stay enabled.

The renewed certificate is re-imported onto the *same* ACM ARN, so the HTTPS
listener configuration is never touched.

## Renewing

```bash
python -m venv .venv
.venv/Scripts/pip install certbot
ACME_EMAIL=you@example.com .venv/Scripts/python scripts/renew-api-cert.py
```

`ACME_EMAIL` is optional and only registers an address that receives expiry
reminders from Let's Encrypt. The script discovers the load balancer, the `:80`
listener and the ACM certificate at run time; there is nothing to configure.

Expected output ends with `re-imported onto the existing certificate; listener
unchanged`. Then verify:

```bash
curl -sS -o /dev/null -w '%{http_code}\n' https://lhsdb-fa-api.piriwin.com/players
# 401 -- the app is reachable over TLS and asking for a JWT, which is correct

echo | openssl s_client -servername lhsdb-fa-api.piriwin.com \
  -connect lhsdb-fa-api.piriwin.com:443 2>/dev/null | grep "Verify return code"
# Verify return code: 0 (ok)
```

The certificate covers `lhsdb-fa-api.piriwin.com` only. HTTP-01 cannot issue
wildcards — which is fine here, and preferable to reusing the main site's key.

## If renewal fails

- **`CAA_ERROR` or an authorisation failure from Let's Encrypt** — check the CAA
  record still lists `letsencrypt.org`:
  `curl -s 'https://dns.google/resolve?name=piriwin.com&type=CAA'`
- **The challenge times out** — confirm the `:80` listener is still enabled and
  publicly reachable: `curl -I http://lhsdb-fa-api.piriwin.com/` should answer
  (404 on `/` is normal, the API has no root route).
- **A stale challenge rule is left behind** after a crash — list and remove it:
  `aws elbv2 describe-rules --region us-east-2 --listener-arn <:80 listener>`;
  anything at priority 1 with a `fixed-response` action is leftover.
- **`several imported certificates match`** — an earlier run created a duplicate.
  Delete the ones not attached to the listener so renewal targets a single ARN.

## The permanent fix

Ask WHC/n0c support to add a second CAA record **alongside** the existing one:

```
piriwin.com.  CAA  0 issue "amazon.com"
```

Do not ask them to replace the Let's Encrypt line — the hosting panel needs it to
renew the main site's certificate.

Once that record exists, request an ACM certificate for the API host normally.
ACM will then issue and auto-renew it forever, and this script and document
become unnecessary.

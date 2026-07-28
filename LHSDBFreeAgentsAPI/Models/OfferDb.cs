using System;
using Amazon.DynamoDBv2.DataModel;

namespace LHSDBFreeAgentsAPI.Models
{
    [DynamoDBTable("Offers")]
    public class OfferDb
    {
        [DynamoDBHashKey]
        [DynamoDBGlobalSecondaryIndexHashKey("PlayerID-OfferedBy-index")]
        public int PlayerID { get; set; }
        [DynamoDBRangeKey]
        [DynamoDBGlobalSecondaryIndexHashKey("TeamID-index")]
        public int TeamID { get; set; }
        [DynamoDBGlobalSecondaryIndexRangeKey("PlayerID-OfferedBy-index")]
        public string OfferedBy { get; set; }
        public bool IsOwner { get; set; }
        public int Amount { get; set; }
        public string PlayerType { get; set; }
        public string PlayerName { get; set; }
    }
}

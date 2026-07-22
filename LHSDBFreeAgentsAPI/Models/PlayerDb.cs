using System;
using Amazon.DynamoDBv2.DataModel;

namespace LHSDBFreeAgentsAPI.Models
{
    [DynamoDBTable("Players")]
    public class PlayerDb
    {
        [DynamoDBHashKey]
        public int UniqueID { get; set; }

        public string Name { get; set; }
        public string URLLink { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey]
        public int Team { get; set; }
        public string AgeDate { get; set; }
        public bool PosC { get; set; }
        public bool PosLW { get; set; }
        public bool PosRW { get; set; }
        public bool PosD { get; set; }
        public int Contract { get; set; }
        public int Salary { get; set; }
        public int CK { get; set; }
        public int FG { get; set; }
        public int DI { get; set; }
        public int SK { get; set; }
        public int ST { get; set; }
        public int EN { get; set; }
        public int DU { get; set; }
        public int PH { get; set; }
        public int FO { get; set; }
        public int PA { get; set; }
        public int SC { get; set; }
        public int DF { get; set; }
        public int PS { get; set; }
        public int EX { get; set; }
        public int LD { get; set; }
        public int PO { get; set; }
        public int Overall { get; set; }

        // Goalie attributes
        public int SZ { get; set; }
        public int AG { get; set; }
        public int RB { get; set; }
        public int HS { get; set; }
        public int RT { get; set; }

        // Calculated attributes
        public int OVK { get; set; }
        public string Position { get; set; }

        public bool IsFA { get; set; }
        public int Age { get; set; }
    }
}

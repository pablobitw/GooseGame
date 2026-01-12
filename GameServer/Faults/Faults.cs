using System.ServiceModel;

namespace GameServer.Faults
{
    public static class Faults
    {
        public static class Types
        {
            public const string Infrastructure = "INFRASTRUCTURE";  
            public const string BusinessRule = "BUSINESS_RULE";    
            public const string Security = "SECURITY";            
            public const string Critical = "CRITICAL_INTERNAL";  
        }

        public static FaultException<ServiceFault> Create(string type, string code, string technicalMessage)
        {
            var fault = new ServiceFault
            {
                Type = type,
                Code = code,
                Message = technicalMessage
            };
            return new FaultException<ServiceFault>(fault, new FaultReason(technicalMessage));
        }
    }
}
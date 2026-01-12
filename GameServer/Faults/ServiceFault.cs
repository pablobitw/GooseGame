using System.Runtime.Serialization;

namespace GameServer.Faults
{
    [DataContract]
    public class ServiceFault
    {

        [DataMember]
        public string Code { get; set; }


        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string Type { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    internal class NameUtils
    {
        public const string DataMapPrefix = "Data_MappedFile_{0}";
        public const string DataReadMutexPrefix = "Data_Read_Mutex_{0}";
        public const string DataWriteMutexPrefix = "Data_Write_Mutex_{0}";
        public const string ImpartiblePrefix = "Impartible_Trans_{0}";
        //
        public const string IdMapPrefix = "IDs_MappedFile_{0}";
        public const string IdMutexPrefix = "IDs_Mutex_{0}";
        public const string OthersMapPrefix = "Share_Other_MappedFile_{0}";
        public const string OthersMutexPrefix = "Share_Other_Mutex_{0}";
        //
        public const string AddEventPrefix = "Add_Instance_Event_{0}_{1}";
        public const string RemoveEventPrefix = "Remove_Instance_Event_{0}_{1}";
        public const string DataChangeEventPrefix = "Data_Changed_Event_{0}_{1}";
        //
        public const string AddPassPrefix = "Add_Event_Pass_{0}_{1}";
        public const string RemovePassPrefix = "Remove_Event_Pass_{0}_{1}";
        public const string DataChangePassPrefix = "Data_Changed_Event_Pass_{0}_{1}";
    }
}

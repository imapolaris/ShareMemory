using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    public class DataChangedEventArgs:SharedDataEventArgs
    {
        public DataChangedEventArgs(int id,byte[] data):base(id)
        {
            NewData = data;
        }

        public byte[] NewData { get; private set; }
    }
}

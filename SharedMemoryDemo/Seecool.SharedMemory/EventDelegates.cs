using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    public delegate void SharedDataEventHandler(object sender, SharedDataEventArgs args);
    public delegate void DataChangedEventHandler(object sender, DataChangedEventArgs args);
}

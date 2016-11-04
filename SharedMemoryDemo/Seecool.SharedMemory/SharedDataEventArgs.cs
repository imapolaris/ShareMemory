using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    public class SharedDataEventArgs : EventArgs
    {
        private int _id;
        internal SharedDataEventArgs(int id)
        {
            _id = id;
        }

        /// <summary>
        /// 引发事件的共享实例Id。
        /// </summary>
        public int InstaceId { get { return _id; } }
    }
}

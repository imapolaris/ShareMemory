using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    internal class SharedEventManager
    {
        private string _sharedName;
        private SharedIdManager _idManager;

        public SharedEventManager(string name, SharedIdManager idMgr)
        {
            _sharedName = name;
            _idManager = idMgr;
        }

        private EventWaitHandle createWaitHandle(string prefix, int id, out bool createNew)
        {
            string name = string.Format(prefix, _sharedName, id);
            EventWaitHandle wh = new EventWaitHandle(false, EventResetMode.AutoReset, name, out createNew);
            return wh;
        }

        private void doNotify(string prefix, string passPrefix, bool isSync)
        {
            _idManager.UpdateModifyId();
            //引发事件并返回无效Id列表。
            List<int> outIds = notifyOnce(prefix, passPrefix, isSync);
            //清除无效ID
            if (outIds.Count > 0)
            {
                _idManager.RemoveIds(outIds);
                foreach (int id in outIds)
                {
                    _idManager.UpdateModifyIdTo(id);
                    notifyOnce(NameUtils.RemoveEventPrefix, NameUtils.RemovePassPrefix, false);
                }
            }
        }

        private List<int> notifyOnce(string prefix, string passPrefix, bool isSync)
        {
            int[] ids = _idManager.GetUsedIds();
            List<int> outIds = new List<int>();
            if (ids.Length > 0)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    bool createNew = true;
                    EventWaitHandle wh = createWaitHandle(prefix, ids[i], out createNew);
                    if (!createNew)
                    {
                        if (isSync)
                        {
                            EventWaitHandle pass = createWaitHandle(passPrefix, ids[i], out createNew);
                            wh.Set();
                            if (!createNew)
                                pass.WaitOne(100);
                            pass.Close();
                        }
                        else {
                            wh.Set();
                        }
                    }
                    else if (ids[i] != _idManager.CurrentId)
                    {
                        outIds.Add(ids[i]);
                    }
                    wh.Close();
                    Thread.Sleep(1);
                }
            }
            return outIds;
        }

        /// <summary>
        /// 通知所有共享线程激发 InstanceAdded 事件。
        /// </summary>
        public void NotifyInstanceAddedEvent()
        {
            doNotify(NameUtils.AddEventPrefix, NameUtils.AddPassPrefix, true);
        }

        /// <summary>
        /// 通知所有共享线程激发 InstanceRemoved 事件。
        /// </summary>
        public void NotifyInstanceRemovedEvent()
        {
            doNotify(NameUtils.RemoveEventPrefix, NameUtils.RemovePassPrefix, false);
        }

        /// <summary>
        /// 通知所有共享线程激发 DataChanged 事件。
        /// </summary>
        public void NotifyDataChangedEvent()
        {
            doNotify(NameUtils.DataChangeEventPrefix, NameUtils.DataChangePassPrefix, true);
        }
    }
}

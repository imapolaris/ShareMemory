using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    internal class SharedIdManager : IDisposable
    {
        MemoryMappedFile _mapId;
        const int _mapIdCapacity = 2 * 1024;
        Mutex _mutexId;
        //
        MemoryMappedFile _mapOthers;
        const int _mapOthersCapacity = 1024;
        Mutex _mutexOthers;
        public SharedIdManager(string name, int initCapacity)
        {
            prepareSyncParam(name);
            getAndUpdateIds();
            getOrUpdateCapacity(initCapacity);
        }

        private void prepareSyncParam(string name)
        {
            _mapId = MemoryMappedFile.CreateOrOpen(string.Format(NameUtils.IdMapPrefix, name), _mapIdCapacity * 4, MemoryMappedFileAccess.ReadWrite);
            _mutexId = new Mutex(false, string.Format(NameUtils.IdMutexPrefix, name));

            _mapOthers = MemoryMappedFile.CreateOrOpen(string.Format(NameUtils.OthersMapPrefix, name), _mapOthersCapacity * 4, MemoryMappedFileAccess.ReadWrite);
            _mutexOthers = new Mutex(false, string.Format(NameUtils.OthersMutexPrefix, name));
        }

        private void doImpartibleAction(Mutex mutex, Action action)
        {
            try
            {
                mutex.WaitOne();
                action();
            }
            catch (AbandonedMutexException)
            {
                //不做处理。
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// 该实例的有效ID标识。
        /// </summary>
        public int CurrentId { get; private set; }

        public int Capacity { get; private set; }

        public int[] GetUsedIds()
        {
            List<int> ids = null;
            doImpartibleAction(_mutexId, () =>
            {
                using (var stream = _mapId.CreateViewStream(0, 0, MemoryMappedFileAccess.ReadWrite))
                {
                    using (BinaryReader br = new BinaryReader(stream))
                        ids = readIds(br);
                }
            });
            return ids.ToArray();
        }

        internal void RemoveIds(List<int> rIDs)
        {
            //Console.WriteLine("removed ids:" + string.Join(",", rIDs));
            doImpartibleAction(_mutexId, () =>
            {
                using (var stream = _mapId.CreateViewStream(0, 0, MemoryMappedFileAccess.ReadWrite))
                {
                    //Read
                    using (BinaryReader br = new BinaryReader(stream))
                    {
                        List<int> ids = readIds(br);
                        foreach (int x in rIDs)
                            ids.Remove(x);
                        //Write
                        using (BinaryWriter bw = new BinaryWriter(stream))
                        {
                            bw.Seek(0, SeekOrigin.Begin);
                            bw.Write(getBytes(ids));
                        }
                    }
                }
            });
        }

        #region 【操作共享Id列表方法】
        private void getAndUpdateIds()
        {
            doImpartibleAction(_mutexId, () =>
             {
                 using (var stream = _mapId.CreateViewStream(0, 0, MemoryMappedFileAccess.ReadWrite))
                 {
                     //Read
                     using (BinaryReader br = new BinaryReader(stream))
                     {
                         List<int> ids = readIds(br);
                         if (ids.Count >= _mapIdCapacity)
                         {
                             br.Close();
                             throw new ArgumentOutOfRangeException("已超出单个共享内存块允许创建的最大实例个数。");
                         }

                         //Set the validID
                         CurrentId = findValidId(ids);

                         //Write
                         using (BinaryWriter bw = new BinaryWriter(stream))
                         {
                             bw.Seek(4 * ids.Count, SeekOrigin.Begin);
                             bw.Write(CurrentId);
                         }
                     }
                 }
             });
        }

        private int findValidId(List<int> ids)
        {
            int id = 0;
            int seed = GetIdSeed();
            if (seed == int.MaxValue)
            {
                for (int i = 0; i <= int.MaxValue; i++)
                {
                    if (!ids.Contains(i))
                    {
                        id = i;
                        break;
                    }
                }
            }
            else {
                id = ++seed;
                UpdateIdSeed(seed);
            }
            return id;
        }

        public void DeleteCurrentId()
        {
            doImpartibleAction(_mutexId, () =>
             {
                 using (var stream = _mapId.CreateViewStream(0, 0, MemoryMappedFileAccess.ReadWrite))
                 {
                     //Read
                     using (BinaryReader br = new BinaryReader(stream))
                     {
                         List<int> ids = readIds(br);

                         //Delete the validId
                         ids.Remove(CurrentId);

                         //Write
                         using (BinaryWriter bw = new BinaryWriter(stream))
                         {
                             bw.Seek(0, SeekOrigin.Begin);
                             bw.Write(getBytes(ids));
                         }
                     }
                 }
             });
        }

        private List<int> readIds(BinaryReader br)
        {
            List<int> ids = new List<int>();
            //Read
            for (int i = 0; i < _mapIdCapacity; i++)
            {
                int id = br.ReadInt32();
                if (id <= 0)
                    break;
                ids.Add(id);
            }
            return ids;
        }

        private byte[] getBytes(List<int> ids)
        {
            byte[] bytes = new byte[_mapIdCapacity * 4];
            int index = 0;
            byte[] subBytes;
            for (int i = 0; i < ids.Count; i++)
            {
                subBytes = BitConverter.GetBytes(ids[i]);
                for (int j = 0; j < subBytes.Length; j++)
                {
                    bytes[index++] = subBytes[j];
                }
            }
            return bytes;
        }
        #endregion 【操作共享Id列表方法】

        #region 【操作modifyId方法】
        public void UpdateModifyId()
        {
            doImpartibleAction(_mutexOthers, () =>
             {
                 using (var stream = _mapOthers.CreateViewStream(0, 4, MemoryMappedFileAccess.Write))
                 {
                     using (BinaryWriter bw = new BinaryWriter(stream))
                     {
                         bw.Write(CurrentId);
                     }
                 }
             });
        }

        public void UpdateModifyIdTo(int id)
        {
            doImpartibleAction(_mutexOthers, () =>
            {
                using (var stream = _mapOthers.CreateViewStream(0, 4, MemoryMappedFileAccess.Write))
                {
                    using (BinaryWriter bw = new BinaryWriter(stream))
                    {
                        bw.Write(id);
                    }
                }
            });
        }

        public int GetModifyId()
        {
            int id = 0;
            doImpartibleAction(_mutexOthers, () =>
             {
                 using (var stream = _mapOthers.CreateViewStream(0, 4, MemoryMappedFileAccess.Read))
                 {
                     using (BinaryReader br = new BinaryReader(stream))
                     {
                         id = br.ReadInt32();
                     }
                 }
             });
            return id;
        }
        #endregion 【操作modifyId方法】

        #region 【操作Capacity方法】
        private void getOrUpdateCapacity(int inCapacity)
        {
            int capa = GetCapacity();
            if (capa <= 0)
            {
                capa = inCapacity;
                UpdateCapacity(capa);
            }
            Capacity = capa;
        }

        private void UpdateCapacity(int capacity)
        {
            doImpartibleAction(_mutexOthers, () =>
            {
                using (var stream = _mapOthers.CreateViewStream(4, 4, MemoryMappedFileAccess.Write))
                {
                    using (BinaryWriter bw = new BinaryWriter(stream))
                    {
                        bw.Write(capacity);
                    }
                }
            });
        }

        private int GetCapacity()
        {
            int id = 0;
            doImpartibleAction(_mutexOthers, () =>
             {
                 using (var stream = _mapOthers.CreateViewStream(4, 4, MemoryMappedFileAccess.Read))
                 {
                     using (BinaryReader br = new BinaryReader(stream))
                     {
                         id = br.ReadInt32();
                     }
                 }
             });
            return id;
        }
        #endregion 【操作Capacity方法】

        #region 【操作ID种子】
        public void UpdateIdSeed(int seed)
        {
            doImpartibleAction(_mutexOthers, () =>
            {
                using (var stream = _mapOthers.CreateViewStream(8, 4, MemoryMappedFileAccess.Write))
                {
                    using (BinaryWriter bw = new BinaryWriter(stream))
                    {
                        bw.Write(seed);
                    }
                }
            });
        }

        public int GetIdSeed()
        {
            int id = 0;
            doImpartibleAction(_mutexOthers, () =>
            {
                using (var stream = _mapOthers.CreateViewStream(8, 4, MemoryMappedFileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(stream))
                    {
                        id = br.ReadInt32();
                    }
                }
            });
            return id;
        }
        #endregion 【操作ID种子】

        #region 【实现IDisposable接口】
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed = false;
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                DeleteCurrentId();
                _mapId.Dispose();
                _mutexId.Dispose();
                _mapOthers.Dispose();
                _mutexOthers.Dispose();
                _disposed = true;
            }
        }

        //该类实例不能由系统析构。
        //~SharedIdManager()
        //{
        //    Dispose(false);
        //}
        #endregion 【实现IDisposable接口】
    }
}

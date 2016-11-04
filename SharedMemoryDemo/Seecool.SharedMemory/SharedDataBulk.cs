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
    /// <summary>
    /// 该类型用于创建一个命名的内存共享实例，执行共享数据读/写，并可在数据改变时获取事件通知。
    /// </summary>
    public class SharedDataBulk : IDisposable
    {
        /// <summary>
        /// 查询指定ID的共享实例是否依然存活。
        /// </summary>
        /// <param name="shareName">共享内存识别名</param>
        /// <param name="instanceId">实例ID</param>
        /// <returns></returns>
        public static bool IsInstanceAlive(string shareName, int instanceId)
        {
            string name = string.Format(NameUtils.AddEventPrefix, shareName, instanceId);
            bool createNew = false;
            EventWaitHandle wh = new EventWaitHandle(false, EventResetMode.AutoReset, name, out createNew);
            wh.Close();
            return !createNew;
        }
        /// <summary>
        /// 单个数据块，可申请的最大共享内存空间大小。暂定为16MB
        /// </summary>
        public const int MaxCapacity = 16 * 1024 * 1024;
        MemoryMappedFile _dataMappedFile;
        Mutex _mutexReader;
        Mutex _mutexWriter;
        Mutex _mutexBulk;
        SharedIdManager _idManager;
        SharedEventManager _eventManager;

        /// <summary>
        /// 生成一个共享内存数据块实例。
        /// </summary>
        /// <param name="shareName">共享内存识别名称，必须为需要共享内存的实例指定相同的名称。</param>
        /// <param name="capacity">
        /// 共享内存容量(以字节为单位)，如果已经存在一个与<paramref name="shareName"/>相同的共享空间实例，
        /// 该参数将被忽略。实际共享内存容量以属性<see cref="Capacity"/>为准。
        /// </param>
        public SharedDataBulk(string shareName, int capacity)
        {
            if (capacity <= 0 || capacity > MaxCapacity)
                throw new OutOfCapacityException("已超出允许的共享内存空间容量范围。");
            if (string.IsNullOrWhiteSpace(shareName))
                throw new ArgumentException("名称不能为空值。");
            if (shareName.Length > 200)
                throw new ArgumentOutOfRangeException("名称长度不能超过200个有效字符。");

            _mutexBulk = new Mutex(false, string.Format(NameUtils.ImpartiblePrefix, shareName));
            doImpartibleAction(_mutexBulk, () =>
             {
                 this.SharedName = shareName;
                 _idManager = new SharedIdManager(shareName, capacity);
                 _eventManager = new SharedEventManager(shareName, _idManager);
                 this.Capacity = _idManager.Capacity; //获得实际的Capacity
                 prepareSyncParam(shareName, this.Capacity);
                 initWaitHandle();
             });

            //通知InstanceAdded事件
            new Thread(() =>
            {
                doImpartibleAction(_mutexBulk, _eventManager.NotifyInstanceAddedEvent);
            })
            { IsBackground = true }.Start();
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
        /// 当前实例所关联的共享内存空间实际容量。
        /// </summary>
        public int Capacity
        {
            get;
            private set;
        }

        /// <summary>
        /// 共享内存识别名称。具有相同的名称的实例，将共享同一块内存空间。
        /// </summary>
        public string SharedName
        {
            get;
            private set;
        }

        /// <summary>
        /// 共享实例Id。
        /// <para>Id索引序号从1开始，自动递增。</para>
        /// </summary>
        public int CurrentId
        {
            get { return _idManager.CurrentId; }
        }

        public int TotalInstance
        {
            get { return _idManager.GetUsedIds().Length; }
        }

        public int[] AllInstanceIds
        {
            get { return _idManager.GetUsedIds(); }
        }

        private void prepareSyncParam(string name, int capacity)
        {
            //创建内存共享文件。
            string sharedDataName = string.Format(NameUtils.DataMapPrefix, name);
            _dataMappedFile = MemoryMappedFile.CreateOrOpen(sharedDataName, capacity, MemoryMappedFileAccess.ReadWrite);
            //创建数据读取互斥体。
            _mutexReader = new Mutex(false, string.Format(NameUtils.DataReadMutexPrefix, name));
            _mutexWriter = new Mutex(false, string.Format(NameUtils.DataWriteMutexPrefix, name));
        }

        /// <summary>
        /// 执行ReadWrite同步操作，防止当Read执行后数据被其他线程/进程更改，导致数据的不一致状态。
        /// </summary>
        /// <param name="readWriteAction">
        /// 可以同时执行<see cref="ReadSharedData"/>和<see cref="WriteSharedData(byte[])"/>的同步操作块，而不会被其他读/写线程中断。
        /// </param>
        public void LockReadWriteAction(Action readWriteAction)
        {
            try
            {
                _mutexBulk.WaitOne();
                readWriteAction();
            }
            catch (AbandonedMutexException)
            {
                //不做处理。
            }
            finally
            {
                _mutexBulk.ReleaseMutex();
            }
        }

        /// <summary>
        /// 读取共享数据内容。
        /// <para>实际读取的字节数组长度与共享内存容量<see cref="Capacity"/>相等。</para>
        /// </summary>
        /// <returns></returns>
        public byte[] ReadSharedData()
        {
            byte[] bytes = null;

            doImpartibleAction(_mutexReader, () =>
            {
                using (var stream = _dataMappedFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(stream))
                        bytes = br.ReadBytes(Capacity);
                }
            });
            return bytes;
        }

        /// <summary>
        /// 更新共享数据内容。
        /// </summary>
        /// <param name="bytes">更新数据内容，数组长度应不大于<see cref="Capacity"/>指定的共享内存最大容量。</param>
        public void WriteSharedData(byte[] bytes)
        {
            if (bytes == null)
                bytes = new byte[0];

            if (bytes.Length > Capacity)
                throw new OutOfCapacityException("待写入数据的字节长度超出共享内存空间的容量。");

            doImpartibleAction(_mutexBulk, () =>
            {
                doImpartibleAction(_mutexReader, () =>
                 {
                     doImpartibleAction(_mutexWriter, () =>
                     {
                         bytes = buildFullBytes(bytes);
                         using (var stream = _dataMappedFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Write))
                         {
                             stream.Write(bytes, 0, bytes.Length);
                         }
                     });
                 });
                _eventManager.NotifyDataChangedEvent();
            });

            ////通知DataChanged事件。
            //new Thread(() =>
            //{
            //    doImpartibleAction(_mutexBulk, _eventManager.NotifyDataChangedEvent);
            //})
            //{ IsBackground = true }.Start();
        }

        private byte[] buildFullBytes(byte[] bytes)
        {
            byte[] outBytes = new byte[Capacity];
            Array.Copy(bytes, outBytes, bytes.Length);
            return outBytes;
        }

        #region 【线程等待句柄】
        private EventWaitHandle _addedPassWH;
        //private EventWaitHandle _removedPassWH;
        private EventWaitHandle _dataChangePassWH;
        private EventWaitHandle _addedWH;
        private EventWaitHandle _removedWH;
        private EventWaitHandle _dataChangeWH;
        private bool _terminate = false;
        private void initWaitHandle()
        {
            _addedPassWH = createHandle(NameUtils.AddPassPrefix);
            //_removedPassWH = createHandle(NameUtils.RemovePassPrefix);
            _dataChangePassWH = createHandle(NameUtils.DataChangePassPrefix);

            _addedWH = createHandle(NameUtils.AddEventPrefix);
            _removedWH = createHandle(NameUtils.RemoveEventPrefix);
            _dataChangeWH = createHandle(NameUtils.DataChangeEventPrefix);

            StartWaitHandle(waitAddEvent);
            StartWaitHandle(waitRemoveEvent);
            StartWaitHandle(waitDataChangedEvent);
        }

        private void StartWaitHandle(ThreadStart start)
        {
            Thread thread = new Thread(start);
            thread.IsBackground = true;
            thread.Start();
        }

        private EventWaitHandle createHandle(string prefix)
        {
            return new EventWaitHandle(false, EventResetMode.AutoReset,
                string.Format(prefix, SharedName, CurrentId));
        }

        private void waitAddEvent()
        {
            _addedWH.WaitOne();
            if (_terminate)
                return;
            int modifyId = _idManager.GetModifyId();
            _addedPassWH.Set();
            OnInstanceAdded(new SharedDataEventArgs(modifyId));
            waitAddEvent();
        }

        private void waitRemoveEvent()
        {
            _removedWH.WaitOne();
            if (_terminate)
                return;
            int modifyId = _idManager.GetModifyId();
            //_removedPassWH.Set();
            OnInstanceRemoved(new SharedDataEventArgs(modifyId));
            waitRemoveEvent();
        }

        private void waitDataChangedEvent()
        {
            _dataChangeWH.WaitOne();
            if (_terminate)
                return;
            int modifyId = _idManager.GetModifyId();
            byte[] data = ReadSharedData();
            _dataChangePassWH.Set();
            OnDataChanged(new DataChangedEventArgs(modifyId, data));
            waitDataChangedEvent();
        }

        private void terminateWaitHandle()
        {
            _terminate = true;
            if (_addedWH != null)
            {
                _addedWH.Set();
                _addedWH.Close();
            }
            if (_removedWH != null)
            {
                _removedWH.Set();
                _removedWH.Close();
            }
            if (_dataChangeWH != null)
            {
                _dataChangeWH.Set();
                _dataChangeWH.Close();
            }

            //_addedPassWH.Set();
            //_removedPassWH.Set();
            //_dataChangePassWH.Set();

            //_removedPassWH.Close();
            if (_addedPassWH != null)
                _addedPassWH.Close();
            if (_dataChangePassWH != null)
                _dataChangePassWH.Close();
        }
        #endregion 【线程等待句柄】

        #region 【事件定义】
        /// <summary>
        /// 有新的共享实例加入事件。
        /// </summary>
        public event SharedDataEventHandler InstanceAdded;
        /// <summary>
        /// 有共享实例被移除事件。
        /// </summary>
        public event SharedDataEventHandler InstanceRemoved;
        /// <summary>
        /// 共享数据改变事件。
        /// </summary>
        public event DataChangedEventHandler DataChanged;
        /// <summary>
        /// 当前实例Dispose执行过程。
        /// </summary>
        public event EventHandler Disposing;

        private void OnInstanceAdded(SharedDataEventArgs args)
        {
            new Thread(() =>
            {
                SharedDataEventHandler handler = InstanceAdded;
                if (handler != null)
                    handler(this, args);
            })
            { IsBackground = true }.Start();
        }

        private void OnInstanceRemoved(SharedDataEventArgs args)
        {
            new Thread(() =>
            {
                SharedDataEventHandler handler = InstanceRemoved;
                if (handler != null)
                    handler(this, args);
            })
            { IsBackground = true }.Start();

        }

        private void OnDataChanged(DataChangedEventArgs args)
        {
            new Thread(() =>
            {
                DataChangedEventHandler handler = DataChanged;
                if (handler != null)
                    handler(this, args);
            })
            { IsBackground = true }.Start();

        }

        private void OnDisposing(EventArgs args)
        {
            EventHandler handler = Disposing;
            if (handler != null)
                handler(this, args);
        }
        #endregion 【事件定义】

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
                terminateWaitHandle();
                OnDisposing(new EventArgs());

                if (_dataMappedFile != null)
                    _dataMappedFile.Dispose();
                if (_mutexWriter != null)
                    _mutexWriter.Close();
                if (_mutexReader != null)
                    _mutexReader.Close();

                //通知InstanceRemoved事件。
                //以下两句代码的顺序不能改变。
                if (_mutexBulk != null)
                {
                    doImpartibleAction(_mutexBulk, _eventManager.NotifyInstanceRemovedEvent);
                    _mutexBulk.Close();
                }
                if (_idManager != null)
                    _idManager.Dispose();

                _disposed = true;
            }
        }

        ~SharedDataBulk()
        {
            Dispose(false);
#if Debug
            Console.WriteLine("SharedDataBulk Finalize:" + CurrentId);
#endif
        }
        #endregion 【实现IDisposable接口】
    }
}

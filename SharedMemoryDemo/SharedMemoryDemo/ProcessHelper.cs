using Seecool.ShareMemory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedMemoryDemo
{
    public static class ProcessHelper
    {
        public const string SharedMemoryName = "SeecoolCCTV_2_0_SharedMemory";
        static SharedDataBulk _bulk;
        const int _maxInstaces = 100;
        private readonly static int _processIndex;
        public static int ProcessIndex
        {
            get { return _processIndex; }
        }

        static ProcessHelper()
        {
            _bulk = new SharedDataBulk(SharedMemoryName, sizeof(int) * 2 * _maxInstaces);
            _processIndex = FindValidIndex();
            if (ProcessIndex <= 0)
            {
                throw new MaxInstanceException();
            }
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            ReleaseDomain();
        }

        private static int CurrentBulkId { get { return _bulk.CurrentId; } }

        public static string FormatKey(string key)
        {
            if (ProcessIndex <= 0)
            {
                throw new MaxInstanceException();
            }
            return string.Format("{0}_{1}_{2}", key, Process.GetCurrentProcess().ProcessName, ProcessIndex);
        }

        private static int FindValidIndex()
        {
            Dictionary<int, int> procIndex = GetRegisteredDomains();
            FilterValidProcesses(procIndex);
            //所有已使用的索引位。
            List<int> usedIndices = new List<int>();
            foreach (int value in procIndex.Values)
                usedIndices.Add(value);

            int rtnIndex = -1;
            for (int i = 1; i <= _maxInstaces; i++)
            {
                if (!usedIndices.Contains(i))
                {
                    rtnIndex = i;
                    break;
                }
            }

            if (rtnIndex > 0)
            {
                procIndex[CurrentBulkId] = rtnIndex;
                //更新共享内存区。
                UpdateSharedMemory(procIndex);
            }
            return rtnIndex;
        }

        //释放当前AppDomain占用的索引位。
        private static void ReleaseDomain()
        {
            Dictionary<int, int> procIndex = GetRegisteredDomains();
            FilterValidProcesses(procIndex);
            if (procIndex.ContainsKey(CurrentBulkId))
            {
                procIndex.Remove(CurrentBulkId);
            }
            UpdateSharedMemory(procIndex);
        }

        private static int getIntFromBytes(byte[] data, ref int startIndex)
        {
            int value = BitConverter.ToInt32(data, startIndex);
            startIndex += 4;
            return value;
        }

        private static void fillBytes(byte[] data, int value, ref int startIndex)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                data[startIndex++] = bytes[i];
            }
        }

        private static Dictionary<int, int> GetRegisteredDomains()
        {
            Dictionary<int, int> procIndex = new Dictionary<int, int>();
            byte[] data = _bulk.ReadSharedData();
            int pos = 0;
            while (true)
            {
                int bulkId = getIntFromBytes(data, ref pos);
                if (bulkId <= 0)
                    break;
                int index = getIntFromBytes(data, ref pos);
                procIndex[bulkId] = index;
            }
            return procIndex;
        }

        private static void UpdateSharedMemory(Dictionary<int, int> procIndex)
        {
            byte[] data = new byte[_bulk.Capacity];
            int startIndex = 0;
            foreach (int bulkId in procIndex.Keys)
            {

                fillBytes(data, bulkId, ref startIndex);
                fillBytes(data, procIndex[bulkId], ref startIndex);
            }
            _bulk.WriteSharedData(data);
        }

        private static void FilterValidProcesses(Dictionary<int, int> procIndex)
        {
            List<int> diedIDs = new List<int>();
            foreach (int bulkId in procIndex.Keys)
            {
                if (!SharedDataBulk.IsInstanceAlive(SharedMemoryName, bulkId))
                    diedIDs.Add(bulkId);
            }
            //清除已结束进程占用的索引位。
            foreach (int bulkId in diedIDs)
            {
                procIndex.Remove(bulkId);
            }
        }
    }

    public class MaxInstanceException : Exception
    {
        public MaxInstanceException() : base(
            "当前应用的启动实例数已达到上限，无法再启动新实例。")
        {

        }

    }
}

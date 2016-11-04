using Seecool.ShareMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedMemoryDemo
{
    class Program
    {
        static SharedDataBulk[] _bulks;
        static void Main(string[] args)
        {
            _bulks = new SharedDataBulk[4];
            for (int i = 0; i < _bulks.Length; i++)
            {
                _bulks[i] = new SharedDataBulk(ConstantParam.TestName, 16);
                _bulks[i].InstanceAdded += Program_InstanceAdded;
                _bulks[i].InstanceRemoved += Program_InstanceRemoved;
                _bulks[i].DataChanged += Program_DataChanged;
            }
            Console.WriteLine("end");
            while (true)
            {
                ConsoleKeyInfo ki = Console.ReadKey();
                Console.WriteLine();
                if (ki.Key == ConsoleKey.A)
                {
                    byte[] bytes = _bulks[0].ReadSharedData();
                    Console.WriteLine(string.Join(",", bytes));
                }
                if (ki.Key == ConsoleKey.C)
                {
                    //独立的读写操作。读写操作分别独立且能保证数据一致性，但读写操作之间可能被
                    //其他线程插入其他操作。
                    _bulks[0].WriteSharedData(new byte[] { 1, 2, 3, 4 });
                    //
                    new Thread(() =>
                    {
                        Thread.Sleep(10);
                        _bulks[0].WriteSharedData(new byte[] { 3, 3, 3, 3, 3 });
                    }).Start();
                    Thread.Sleep(1000);
                    //此时数据可能已被其他线程更改。
                    //
                    byte[] bytes = _bulks[0].ReadSharedData();
                    Console.WriteLine(string.Join(",", bytes)); //输出 3,3,3,3,3
                }
                if (ki.Key == ConsoleKey.S)
                {
                    //锁定的读写同步操作。对方法块加锁，在方法退出前，其他线程无法写入数据。
                    _bulks[0].LockReadWriteAction(() =>
                    {
                        _bulks[0].WriteSharedData(new byte[] { 1, 2, 3, 4 });
                        //
                        new Thread(() =>
                        {
                            Thread.Sleep(10);
                            //词句代码会阻塞至外侧代码块执行完毕，方才开始执行。
                            _bulks[0].WriteSharedData(new byte[] { 7,7,7,7,7 });
                        }).Start();
                        Thread.Sleep(1000);
                        //无论等待多少时长，都不会被其他写数据线程插入。
                        //
                        byte[] bytes = _bulks[0].ReadSharedData();
                        Console.WriteLine(string.Join(",", bytes)); //输出 1,2,3,4
                    });
                }
                else if (ki.Key == ConsoleKey.R)
                    _bulks[2].Dispose(); //执行Dispose,可以保证共享实例被立即释放。
                else if (ki.Key == ConsoleKey.N)
                {
                    Thread t = new Thread(() =>
                     {
                         AppDomain domain = AppDomain.CreateDomain("NewDomain");
                         domain.ExecuteAssembly("WpfUIDemo.exe");
                         AppDomain.Unload(domain);
                     });
                    t.SetApartmentState(ApartmentState.STA);
                    t.Start();

                }
                else if (ki.Key == ConsoleKey.T)
                    Console.WriteLine("InstanceCount:" + _bulks[0].TotalInstance);
                else if (ki.Key == ConsoleKey.E)
                    break;
            }
        }

        private static void Program_DataChanged(object sender, SharedDataEventArgs args)
        {
            Console.WriteLine(string.Format("DataChanged______currentID:{0}__EventSrcID:{1}", (sender as SharedDataBulk).CurrentId, args.InstaceId));
        }

        private static void Program_InstanceRemoved(object sender, SharedDataEventArgs args)
        {
            Console.WriteLine(string.Format("Removed------currentID:{0}__EventSrcID:{1}", (sender as SharedDataBulk).CurrentId, args.InstaceId));
        }

        private static void Program_InstanceAdded(object sender, SharedDataEventArgs args)
        {
            if ((sender as SharedDataBulk).CurrentId != args.InstaceId)
            {
                Console.WriteLine(string.Format("InstanceAdded...........currentID_{0},EventSrcID_{1}", (sender as SharedDataBulk).CurrentId, args.InstaceId));
            }
        }
    }
}

using Seecool.ShareMemory;
using SharedMemoryDemo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace WpfUIDemo
{
    public class MainViewModel
    {
        private List<SharedDataBulk> _bulkList;

        public MainViewModel()
        {
            AddInfos = new ObservableCollection<string>();
            RemoveInfos = new ObservableCollection<string>();
            ChangeInfos = new ObservableCollection<string>();
            AddCmd = new CommandDelegate(_ => { doAdd(); });
            RemoveCmd = new CommandDelegate(_ => { doRemove(); });
            ChangeDataCmd = new CommandDelegate(_ => { doChangeData(); });
            StartNewApp = new CommandDelegate(_ => { doStartNewApp(); });
            //
            _bulkList = new List<SharedDataBulk>();
        }

        public void Init()
        {
            _bulkList.Add(createShareBulk());
        }

        private SharedDataBulk createShareBulk()
        {
            //容量参数16并不一定生效，详情请查看注释。
            //获取真实容量可使用:SharedDataBulk.Capacity
            SharedDataBulk bulk = new SharedDataBulk(ConstantParam.TestName, 16);
            bulk.InstanceAdded += Bulk_InstanceAdded;
            bulk.InstanceRemoved += Bulk_InstanceRemoved;
            bulk.DataChanged += Bulk_DataChanged;
            return bulk;
        }

        private void Bulk_DataChanged(object sender, SharedDataEventArgs args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChangeInfos.Add(string.Format("DataChanged:ListenerID_{0},MessagerID_{1}", (sender as SharedDataBulk).CurrentId, args.InstaceId));
            });
        }

        private void Bulk_InstanceRemoved(object sender, SharedDataEventArgs args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RemoveInfos.Add(string.Format("InstanceRemoved:ListenerID_{0},MessagerID_{1}", (sender as SharedDataBulk).CurrentId, args.InstaceId));
            });
        }

        private void Bulk_InstanceAdded(object sender, SharedDataEventArgs args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AddInfos.Add(string.Format("NewInstance:ListenerID_{0},MessagerID_{1}", (sender as SharedDataBulk).CurrentId, args.InstaceId));
            });
        }

        public ObservableCollection<string> AddInfos { get; set; }
        public ObservableCollection<string> RemoveInfos { get; set; }
        public ObservableCollection<string> ChangeInfos { get; set; }

        public ICommand AddCmd { get; set; }
        public ICommand RemoveCmd { get; set; }
        public ICommand ChangeDataCmd { get; set; }
        public ICommand StartNewApp { get; set; }

        private void doAdd()
        {
            _bulkList.Add(createShareBulk());
        }

        private void doRemove()
        {
            if (_bulkList.Count > 0)
            {
                SharedDataBulk bulk = _bulkList.Last();
                _bulkList.Remove(bulk);
                bulk.Dispose();
            }
        }

        private void doChangeData()
        {
            if (_bulkList.Count > 0)
            {
                SharedDataBulk bulk = _bulkList.Last();
                //获取原有的共享数据。
                byte[] oldData = bulk.ReadSharedData();
                Console.WriteLine(string.Join(",", oldData));

                int cap = bulk.Capacity;
                byte[] bytes = new byte[cap];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)(i % 255);
                }
                bulk.WriteSharedData(bytes);
            }
        }

        private void doStartNewApp()
        {
            Thread t = new Thread(() =>
             {
                 AppDomain domain = AppDomain.CreateDomain("AConsoleApp");
                 domain.ExecuteAssembly("SharedMemoryDemo.exe");
                 AppDomain.Unload(domain);
             })
            { IsBackground = true };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
    }
}

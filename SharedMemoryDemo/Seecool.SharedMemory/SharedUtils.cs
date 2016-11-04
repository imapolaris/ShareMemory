using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Seecool.ShareMemory
{
    internal class SharedUtils
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct USER_INFO_0
        {
            public string UserName;
        }

        [DllImport("Netapi32.dll")]
        extern static int NetUserEnum(
             [MarshalAs(UnmanagedType.LPWStr)]
              string servername,
             int level,
             int filter,
             out IntPtr bufptr,
             int prefmaxlen,
             out int entriesread,
             out int totalentries,
             out int resume_handle);

        [DllImport("Netapi32.dll")]
        extern static int NetApiBufferFree(IntPtr Buffer);

        [DllImport("Advapi32.dll", EntryPoint = "GetUserName", ExactSpelling = false, SetLastError = true)]
        static extern bool GetUserName(
            [MarshalAs(UnmanagedType.LPArray)] byte[] lpBuffer,
            [MarshalAs(UnmanagedType.LPArray)] Int32[] nSize);

        public static List<string> GetAllUsersOfSystem()
        {
            List<string> users = new List<string>();

            int EntriesRead;
            int TotalEntries;
            int Resume;
            IntPtr bufPtr;

            NetUserEnum(null, 0, 2, out bufPtr, -1, out EntriesRead, out TotalEntries, out Resume);

            if (EntriesRead > 0)
            {
                USER_INFO_0[] Users = new USER_INFO_0[EntriesRead];
                IntPtr iter = bufPtr;
                for (int i = 0; i < EntriesRead; i++)
                {
                    Users[i] = (USER_INFO_0)Marshal.PtrToStructure(iter, typeof(USER_INFO_0));
                    iter = (IntPtr)((int)iter + Marshal.SizeOf(typeof(USER_INFO_0)));
                    users.Add(Users[i].UserName);
                }
                NetApiBufferFree(bufPtr);
            }

            return users;
        }

        public static string GetCurrentUserOfSystemByAPI()
        {
            byte[] bytes = new byte[256];
            Int32[] len = new Int32[1];
            len[0] = 256;
            GetUserName(bytes, len);

            return Encoding.ASCII.GetString(bytes).Trim('\0');
        }

        public static string GetCurrentUserOfSystem()
        {
            return WindowsIdentity.GetCurrent().Name;
        }

        static void Main(string[] args)
        {
            List<string> users = GetAllUsersOfSystem();

            Console.WriteLine("All users of System:");
            foreach (string user in users)
            {
                Console.WriteLine(user);
            }
            //Administrator
            //Guest
            //Peter
            //v-petz

            Console.WriteLine("The logged user of the System(by API):");
            Console.WriteLine(GetCurrentUserOfSystemByAPI()); //Peter
            Console.WriteLine("The logged user of the System(by .net method):");
            Console.WriteLine(GetCurrentUserOfSystem());      //Peter-PC\Peter
        }
    }
}

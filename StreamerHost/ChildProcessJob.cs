using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StreamerHost
{
    internal sealed class ChildProcessJob : IDisposable
    {
        private const uint JobObjectExtendedLimitInformationClass = 9;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private IntPtr _handle;

        public ChildProcessJob()
        {
            _handle = CreateJobObject(IntPtr.Zero, null);
            if (_handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the FFmpeg process job.");
            }

            var limits = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JobObjectLimitKillOnJobClose
                }
            };
            if (!SetInformationJobObject(
                    _handle,
                    JobObjectExtendedLimitInformationClass,
                    ref limits,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                int error = Marshal.GetLastWin32Error();
                Dispose();
                throw new Win32Exception(error, "Could not configure FFmpeg process cleanup.");
            }
        }

        public void Assign(Process process)
        {
            if (!AssignProcessToJobObject(_handle, process.Handle))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not attach FFmpeg to its cleanup job.");
            }
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            uint informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}

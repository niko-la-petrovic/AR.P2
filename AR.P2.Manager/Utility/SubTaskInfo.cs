using System;

namespace AR.P2.Manager.Utility
{
    public struct SubTaskInfo : IComparable
    {
        public int TaskIndex { get; set; }
        public int WindowIndex { get; set; }

        public override string ToString()
        {
            return $"{nameof(TaskIndex)}: {TaskIndex}, {nameof(WindowIndex)}: {WindowIndex}";
        }

        public static int Compare(SubTaskInfo x, SubTaskInfo y)
        {
            if (x.TaskIndex > y.TaskIndex)
                return 1;
            else if (x.TaskIndex < y.TaskIndex)
                return -1;
            else
            {
                if (x.WindowIndex > y.WindowIndex)
                    return 1;
                else if (x.WindowIndex < y.WindowIndex)
                    return -1;
                else
                    return 0;
            }
        }

        public int CompareTo(object obj)
        {
            if (obj is SubTaskInfo other)
                return Compare(this, other);
            return -1;
        }

        public override bool Equals(object obj)
        {
            return obj is SubTaskInfo info &&
                   TaskIndex == info.TaskIndex &&
                   WindowIndex == info.WindowIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TaskIndex, WindowIndex);
        }
    }
}
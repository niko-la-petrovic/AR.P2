namespace AR.P2.Manager.Utility
{
    public struct SubTaskInfo
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
                return x.WindowIndex - y.WindowIndex;
        }
    }
}
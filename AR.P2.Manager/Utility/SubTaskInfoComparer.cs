using System.Collections.Generic;

namespace AR.P2.Manager.Utility
{
    public class SubTaskInfoComparer : IComparer<SubTaskInfo>
    {
        public int Compare(SubTaskInfo x, SubTaskInfo y)
        {
            return SubTaskInfo.Compare(x, y);
        }
    }
}

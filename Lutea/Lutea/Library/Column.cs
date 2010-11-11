using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.Library
{
    public enum ColumnType{
        Fileinfo,
        Statistic,
        Tag,
    }
    public struct Column
    {
        public string nameDB;
        public string nameDisplay;
        public string asTagTextApe;
        public string asTagTextID3V24;
        public ColumnType columnType;
        public bool multipleValue;
    }
}

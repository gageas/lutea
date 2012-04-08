using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.Library
{
    public enum LibraryColumnType
    {
        FileName,
        TrackNumber,
        Integer,
        Time,
        Timestamp64,
        Text,
        Rating,
        Bitrate,
        FileSize,
    }
    public class Column
    {
        public Column(
            string DBText,
            string LocalText, 
            LibraryColumnType type = LibraryColumnType.Text,
            bool IsPrimaryKey = false,
            string MappedTagField = null,
            bool IsTextSearchTarget = false,
            bool OmitOnImport = false
            )
        {
            this.DBText = DBText;
            this.LocalText = LocalText;
            this.type = type;
            this.PrimaryKey = IsPrimaryKey;
            this.MappedTagField = MappedTagField;
            this.IsTextSearchTarget = IsTextSearchTarget;
            this.OmitOnImport = OmitOnImport;
        }
        public readonly LibraryColumnType type = LibraryColumnType.Text;
        public readonly string DBText = null;
        public readonly string LocalText = null;
        public readonly bool PrimaryKey = false;
        public readonly string MappedTagField = null;
        public readonly bool IsTextSearchTarget = false;
        public readonly bool OmitOnImport = false;
    }
}

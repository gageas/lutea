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
    public class Column : ICloneable, IEquatable<Column>
    {
        internal Boolean Freeze
        {
            get;
            set;
        }

        public readonly LibraryColumnType Type = LibraryColumnType.Text;

        private string name = null;
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                if (!Freeze)
                {
                    name = value;
                }
            }
        }

        private string localText = null;
        public string LocalText
        {
            get
            {
                return localText;
            }
            set
            {
                if (!Freeze)
                {
                    localText = value;
                }
            }
        }

        /// <summary>
        /// 主キーフラグ。readonly
        /// </summary>
        public readonly bool PrimaryKey = false;

        private string mappedTagField = null;
        public string MappedTagField
        {
            get
            {
                return mappedTagField;
            }
            set
            {
                if (!Freeze)
                {
                    mappedTagField = value;
                }
            }
        }

        private bool isTextSearchTarget = false;
        public bool IsTextSearchTarget
        {
            get
            {
                return isTextSearchTarget;
            }
            set
            {
                if (!Freeze)
                {
                    isTextSearchTarget = value;
                }
            }
        }

        public readonly bool OmitOnImport = false;

        public Column() {
            this.Freeze = false;
        }

        public Column(
            string Name,
            string LocalText, 
            LibraryColumnType type = LibraryColumnType.Text,
            bool IsPrimaryKey = false,
            string MappedTagField = null,
            bool IsTextSearchTarget = false,
            bool OmitOnImport = false
            )
        {
            this.Name = Name;
            this.LocalText = LocalText;
            this.Type = type;
            this.PrimaryKey = IsPrimaryKey;
            this.MappedTagField = MappedTagField;
            this.IsTextSearchTarget = IsTextSearchTarget;
            this.OmitOnImport = OmitOnImport;

            this.Freeze = true;
        }

        public override string ToString()
        {
            return Name;
        }

        public object Clone()
        {
            var clone = (Column)this.MemberwiseClone();
            clone.Freeze = false;
            return clone;
        }

        bool IEquatable<Column>.Equals(Column other)
        {
            if (this.name.Trim().Equals(other.name.Trim())) return true;
            return false;
        }
    }
}

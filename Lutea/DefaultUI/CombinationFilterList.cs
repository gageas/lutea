using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    class CombinationFilterList : ListBox
    {
        private string BindedTag;
        private Yomigana Yomigana;
        public CombinationFilterList Super { get; set; }
        public CombinationFilterList Child { get; set; }
        public IEnumerable<string> WherePhrase
        {
            get
            {
                var strs = new List<string>();
                for (int i = 0; i < SelectedIndices.Count; i++)
                {
                    if (SelectedIndices[i] == 0) continue;
                    strs.Add("'" + Util.Util.EscapeSingleQuotSQL(SelectedItems[i].ToString()) + "'");
                }
                var SuperPhrase = (Super == null ? new List<string>() : Super.WherePhrase);
                if(SelectedIndex <=0)return SuperPhrase;
                return SuperPhrase.Concat(new string[] { "any(" + BindedTag + "," + String.Join(",", strs.ToArray()) + ")" });
            }
        }

        private void SetItems()
        {
            Items.Clear();
            var wherePhraseStr = (Super == null ? null : String.Join(" AND ", Super.WherePhrase));
            var values = Controller.FetchColumnValueMultipleValue(BindedTag, wherePhraseStr).Select(_ => _.Key).OrderBy(_ => Yomigana.GetFirst(_)).ToArray();
            Items.Add("全て(" + values.Count() + ")");
            Items.AddRange(values);
        }

        public void DeliveredUpdate(bool PlayOnCreate)
        {
            SetItems();
            DeliverUpdateToChild(PlayOnCreate);
        }

        private void DeliverUpdateToChild(bool PlayOnCreate)
        {
            if (Child != null)
            {
                Child.DeliveredUpdate(PlayOnCreate);
            }
            else
            {
                var wherePhraseStr = String.Join(" AND ", WherePhrase);
                Lutea.Core.Controller.CreatePlaylist("SELECT * FROM list " + (string.IsNullOrEmpty(wherePhraseStr) ? "" : ("WHERE " + wherePhraseStr)) + ";", PlayOnCreate);
            }
        }

        public CombinationFilterList(string tag, Yomigana yomigana)
        {
            BindedTag = tag;
            Yomigana = yomigana;
            // Disable Smooth Scroll
            SystemParametersInfo(SPI_SETLISTBOXSMOOTHSCROLLING, 0, IntPtr.Zero, 0);

            SelectedIndexChanged += (_, __) => { DeliverUpdateToChild(false); };
            DoubleClick += (_, __) => { DeliverUpdateToChild(true); };
            SetItems();
        }

        private const uint SPI_SETLISTBOXSMOOTHSCROLLING = 0x1007;
        [DllImport("user32.dll")]
        private static extern void SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    }
}

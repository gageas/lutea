using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Tags
{
    public class MetaTag
    {
        private static List<KeyValuePair<string, object>> ID3ToTag(ID3V2Tag.id3tag id3)
        {
            if (id3 == null) return null;
            List<KeyValuePair<string, object>> tag = new List<KeyValuePair<string, object>>();
            id3.frame.ForEach((x) => {
                if (ID3V2Tag.FRAMES[x.id_x].name4 == "TXXX")
                {
                    tag.Add(new KeyValuePair<string, object>(x.extid, x.extvalue));
                }
                else if (ID3V2Tag.FRAMES[x.id_x].name4 == "COMM" && !string.IsNullOrEmpty(x.extid))
                {
                    tag.Add(new KeyValuePair<string, object>(x.extid, x.extvalue));
                }
                else
                {
                    tag.Add(new KeyValuePair<string, object>(ID3V2Tag.FRAMES[x.id_x].asApe, x.value));
                }
            });
            return tag;
        }

        public static List<KeyValuePair<string, object>> readTagByFilename(string filename, bool createImageObject)
        {
            List<KeyValuePair<string, object>> tag = null;
            try
            {
                using (System.IO.FileStream fs = System.IO.File.Open(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    switch (System.IO.Path.GetExtension(filename).ToLower())
                    {
                        case ".flac":
                            tag = FlacTag.Read(fs);
                            break;

                        case ".ogg":
                            tag = Ogg.Read(fs);
                            break;

                        case ".m4a":
                        case ".m4v":
                        case ".mp4":
                        case ".aac":
                            tag = MP4.Read(fs, createImageObject);
                            break;

                        case ".wma":
                        case ".asf":
                        case ".wmv":
                            tag = ASF.Read(fs, createImageObject);
                            break;

                        case ".ape":
                        case ".tak":
                        case ".wv":
                            tag = ApeTag.Read(fs, createImageObject);
                            break;

                        case ".tta":
                        case ".mp3":
                        case ".mp2":
                            try
                            {
                                tag = ApeTag.Read(fs, createImageObject);
                                if (tag == null) tag = new List<KeyValuePair<string, object>>();
                            }
                            catch { }
                            try
                            {
                                fs.Seek(0, System.IO.SeekOrigin.Begin);
                                var tag_id3v2 = ID3ToTag(ID3V2Tag.read_id3tag(fs, createImageObject));
                                if (tag_id3v2 != null) tag.AddRange(tag_id3v2);
                            }
                            catch { }
                            try
                            {
                                fs.Seek(0, System.IO.SeekOrigin.Begin);
                                var tag_id3v1 = ID3.Read(fs);
                                if (tag_id3v1 != null) tag.AddRange(tag_id3v1);
                            }
                            catch { }
                            break;

                        default:
                            return null;
                    }
                }
            }
            catch { }

            if (tag == null) return null;

            // ARTISTがないとき、ALBUMARTISTをARTISTとして扱う
            if (tag.Find((e) => { return e.Key == "ARTIST"; }).Value == null)
            {
                var albumartist = tag.Find((e) => { return e.Key == "ALBUMARTIST"; });
                if (albumartist.Value != null)
                {
                    tag.Add(new KeyValuePair<string, object>("ARTIST", albumartist.Value));
                }
            }
            return tag;
        }
    }
}

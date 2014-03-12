using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Tags
{
    class ID3
    {
        #region ID3 Genre Strings
        private static readonly string[] genreString = new string[148]{
            "Blues",
            "Classic Rock",
            "Country",
            "Dance",
            "Disco",
            "Funk",
            "Grunge",
            "Hip-Hop",
            "Jazz",
            "Metal",
            "New Age",
            "Oldies",
            "Other",
            "Pop",
            "R&B",
            "Rap",
            "Reggae",
            "Rock",
            "Techno",
            "Industrial",
            "Alternative",
            "Ska",
            "Death Metal",
            "Pranks",
            "Soundtrack",
            "Euro-Techno",
            "Ambient",
            "Trip-Hop",
            "Vocal",
            "Jazz+Funk",
            "Fusion",
            "Trance",
            "Classical",
            "Instrumental",
            "Acid",
            "House",
            "Game",
            "Sound Clip",
            "Gospel",
            "Noise",
            "AlternRock",
            "Bass",
            "Soul",
            "Punk",
            "Space",
            "Meditative",
            "Instrumental Pop",
            "Instrumental Rock",
            "Ethnic",
            "Gothic",
            "Darkwave",
            "Techno-Industrial",
            "Electronic",
            "Pop-Folk",
            "Eurodance",
            "Dream",
            "Southern Rock",
            "Comedy",
            "Cult",
            "Gangsta",
            "Top 40",
            "Christian Rap",
            "Pop/Funk",
            "Jungle",
            "Native American",
            "Cabaret",
            "New Wave",
            "Psychadelic",
            "Rave",
            "Showtunes",
            "Trailer",
            "Lo-Fi",
            "Tribal",
            "Acid Punk",
            "Acid Jazz",
            "Polka",
            "Retro",
            "Musical",
            "Rock & Roll",
            "Hard Rock",
            //   The following genres are Winamp extensions
            "Folk",
            "Folk-Rock",
            "National Folk",
            "Swing",
            "Fast Fusion",
            "Bebob",
            "Latin",
            "Revival",
            "Celtic",
            "Bluegrass",
            "Avantgarde",
            "Gothic Rock",
            "Progressive Rock",
            "Psychedelic Rock",
            "Symphonic Rock",
            "Slow Rock",
            "Big Band",
            "Chorus",
            "Easy Listening",
            "Acoustic",
            "Humour",
            "Speech",
            "Chanson",
            "Opera",
            "Chamber Music",
            "Sonata",
            "Symphony",
            "Booty Bass",
            "Primus",
            "Porn Groove",
            "Satire",
            "Slow Jam",
            "Club",
            "Tango",
            "Samba",
            "Folklore",
            "Ballad",
            "Power Ballad",
            "Rhythmic Soul",
            "Freestyle",
            "Duet",
            "Punk Rock",
            "Drum Solo",
            "A capella",
            "Euro-House",
            "Dance Hall",

            "Goa",
            "Drum & Bass",
            "Club-House",
            "Hardcore",
            "Terror",
            "Indie",
            "BritPop",
            "Negerpunk",
            "Polsk Punk",
            "Beat",
            "Christian Gangsta",
            "Heavy Metal",
            "Black Metal",
            "Crossover",
            "Contemporary C",
            "Christian Rock",
            "Merengue",
            "Salsa",
            "Thrash Metal",
            "Anime",
            "JPop",
            "SynthPop",
        };
        #endregion

        public static string GetGenreString(int id)
        {
            if (id < 0) return null;
            if (id >= genreString.Length) return null;
            return genreString[id];
        }

        private static readonly char[] trimChars = new char[2] { ' ', '\0' };
        private static string GetText(byte[]buffer, int offset,int count)
        {
            var text = Encoding.Default.GetString(buffer, offset, count).Trim(trimChars);
            if (text.Length > 0) return text;
            return null;
        }

        public static List<KeyValuePair<string,object>> Read(Stream strm){
            strm.Seek(-128, SeekOrigin.End);
            byte[] buffer = new byte[128];
            strm.Read(buffer,0,128);
            if (buffer[0] != 'T' || buffer[1] != 'A' || buffer[2] != 'G') return null;
            var tag = new List<KeyValuePair<string, object>>();

            var title = GetText(buffer,3,30);
            if (title != null) tag.Add(new KeyValuePair<string, object>("TITLE", title));

            var artist = GetText(buffer, 33, 30);
            if (artist != null) tag.Add(new KeyValuePair<string, object>("ARTIST", artist));

            var album = GetText(buffer, 63, 30);
            if (album != null) tag.Add(new KeyValuePair<string, object>("ALBUM", album));

            var date = GetText(buffer, 93, 4);
            if (date != null) tag.Add(new KeyValuePair<string, object>("DATE", date));

            if (buffer[125] == 0 && buffer[126] != 0) // v1.1
            {
                var comment = GetText(buffer, 97, 28);
                if (comment != null) tag.Add(new KeyValuePair<string, object>("COMMENT", comment));

                tag.Add(new KeyValuePair<string, object>("TRACK", buffer[126].ToString()));
            }
            else // v1.0
            {
                var comment = GetText(buffer, 97, 30);
                if (comment != null) tag.Add(new KeyValuePair<string, object>("COMMENT", comment));
            }

            var genre = GetGenreString(buffer[127]);
            if (genre != null) tag.Add(new KeyValuePair<string, object>("GENRE", genre));
            return tag;
        }
    }
}

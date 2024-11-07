using System;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;

namespace NeoSmart.Web
{
    public static partial class Normalization
    {
        // Based off the work of Emmanuel Vaïsse
        static private readonly Dictionary<int, string> TranslationTable = new Dictionary<int, string>() {
            {306, "IJ"}, {214, "O"}, {338, "O"}, {220, "U"}, {228, "ae"}, {230, "ae"},
            {307, "ij"}, {246, "o"}, {339, "oe"}, {252, "u"}, {223, "ss"}, {383, "ss"},
            {192, "A"}, {193, "A"}, {194, "A"}, {195, "A"}, {196, "A"}, {197, "A"},
            {198, "AE"}, {256, "A"}, {260, "A"}, {258, "A"}, {199, "C"}, {262, "C"},
            {268, "C"}, {264, "C"}, {266, "C"}, {270, "D"}, {272, "D"}, {200, "E"},
            {201, "E"}, {202, "E"}, {203, "E"}, {274, "E"}, {280, "E"}, {282, "E"},
            {276, "E"}, {278, "E"}, {284, "G"}, {286, "G"}, {288, "G"}, {290, "G"},
            {292, "H"}, {294, "H"}, {204, "I"}, {205, "I"}, {206, "I"}, {207, "I"},
            {298, "I"}, {296, "I"}, {300, "I"}, {302, "I"}, {304, "I"}, {308, "J"},
            {310, "K"}, {317, "K"}, {313, "K"}, {315, "K"}, {319, "K"}, {321, "L"},
            {209, "N"}, {323, "N"}, {327, "N"}, {325, "N"}, {330, "N"}, {210, "O"},
            {211, "O"}, {212, "O"}, {213, "O"}, {216, "O"}, {332, "O"}, {336, "O"},
            {334, "O"}, {340, "R"}, {344, "R"}, {342, "R"}, {346, "S"}, {350, "S"},
            {348, "S"}, {536, "S"}, {352, "S"}, {356, "T"}, {354, "T"}, {358, "T"},
            {538, "T"}, {217, "U"}, {218, "U"}, {219, "U"}, {362, "U"}, {366, "U"},
            {368, "U"}, {364, "U"}, {360, "U"}, {370, "U"}, {372, "W"}, {374, "Y"},
            {376, "Y"}, {221, "Y"}, {377, "Z"}, {379, "Z"}, {381, "Z"}, {224, "a"},
            {225, "a"}, {226, "a"}, {227, "a"}, {257, "a"}, {261, "a"}, {259, "a"},
            {229, "a"}, {231, "c"}, {263, "c"}, {269, "c"}, {265, "c"}, {267, "c"},
            {271, "d"}, {273, "d"}, {232, "e"}, {233, "e"}, {234, "e"}, {235, "e"},
            {275, "e"}, {281, "e"}, {283, "e"}, {277, "e"}, {279, "e"}, {402, "f"},
            {285, "g"}, {287, "g"}, {289, "g"}, {291, "g"}, {293, "h"}, {295, "h"},
            {236, "i"}, {237, "i"}, {238, "i"}, {239, "i"}, {299, "i"}, {297, "i"},
            {301, "i"}, {303, "i"}, {305, "i"}, {309, "j"}, {311, "k"}, {312, "k"},
            {322, "l"}, {318, "l"}, {314, "l"}, {316, "l"}, {320, "l"}, {241, "n"},
            {324, "n"}, {328, "n"}, {326, "n"}, {329, "n"}, {331, "n"}, {242, "o"},
            {243, "o"}, {244, "o"}, {245, "o"}, {248, "o"}, {333, "o"}, {337, "o"},
            {335, "o"}, {341, "r"}, {345, "r"}, {343, "r"}, {347, "s"}, {353, "s"},
            {357, "t"}, {249, "u"}, {250, "u"}, {251, "u"}, {363, "u"}, {367, "u"},
            {369, "u"}, {365, "u"}, {361, "u"}, {371, "u"}, {373, "w"}, {255, "y"},
            {253, "y"}, {375, "y"}, {380, "z"}, {378, "z"}, {382, "z"}, {913, "A"},
            {902, "A"}, {7944, "A"}, {7945, "A"}, {7946, "A"}, {7947, "A"}, {7948, "A"},
            {7949, "A"}, {7950, "A"}, {7951, "A"}, {8072, "A"}, {8073, "A"}, {8074, "A"},
            {8075, "A"}, {8076, "A"}, {8077, "A"}, {8078, "A"}, {8079, "A"}, {8120, "A"},
            {8121, "A"}, {8122, "A"}, {8124, "A"}, {914, "B"}, {915, "G"}, {916, "D"},
            {917, "E"}, {904, "E"}, {7960, "E"}, {7961, "E"}, {7962, "E"}, {7963, "E"},
            {7964, "E"}, {7965, "E"}, {8136, "E"}, {918, "Z"}, {919, "I"}, {905, "I"},
            {7976, "I"}, {7977, "I"}, {7978, "I"}, {7979, "I"}, {7980, "I"}, {7981, "I"},
            {7982, "I"}, {7983, "I"}, {8088, "I"}, {8089, "I"}, {8090, "I"}, {8091, "I"},
            {8092, "I"}, {8093, "I"}, {8094, "I"}, {8095, "I"}, {8138, "I"}, {8140, "I"},
            {920, "T"}, {921, "I"}, {906, "I"}, {938, "I"}, {7992, "I"}, {7993, "I"},
            {7994, "I"}, {7995, "I"}, {7996, "I"}, {7997, "I"}, {7998, "I"}, {7999, "I"},
            {8152, "I"}, {8153, "I"}, {8154, "I"}, {922, "K"}, {923, "L"}, {924, "M"},
            {925, "N"}, {926, "K"}, {927, "O"}, {908, "O"}, {8008, "O"}, {8009, "O"},
            {8010, "O"}, {8011, "O"}, {8012, "O"}, {8013, "O"}, {8184, "O"}, {928, "P"},
            {929, "R"}, {8172, "R"}, {931, "S"}, {932, "T"}, {933, "Y"}, {910, "Y"},
            {939, "Y"}, {8025, "Y"}, {8027, "Y"}, {8029, "Y"}, {8031, "Y"}, {8168, "Y"},
            {8169, "Y"}, {8170, "Y"}, {934, "F"}, {935, "X"}, {936, "P"}, {937, "O"},
            {911, "O"}, {8040, "O"}, {8041, "O"}, {8042, "O"}, {8043, "O"}, {8044, "O"},
            {8045, "O"}, {8046, "O"}, {8047, "O"}, {8104, "O"}, {8105, "O"}, {8106, "O"},
            {8107, "O"}, {8108, "O"}, {8109, "O"}, {8110, "O"}, {8111, "O"}, {8186, "O"},
            {8188, "O"}, {945, "a"}, {940, "a"}, {7936, "a"}, {7937, "a"}, {7938, "a"},
            {7939, "a"}, {7940, "a"}, {7941, "a"}, {7942, "a"}, {7943, "a"}, {8064, "a"},
            {8065, "a"}, {8066, "a"}, {8067, "a"}, {8068, "a"}, {8069, "a"}, {8070, "a"},
            {8071, "a"}, {8048, "a"}, {8112, "a"}, {8113, "a"}, {8114, "a"}, {8115, "a"},
            {8116, "a"}, {8118, "a"}, {8119, "a"}, {946, "b"}, {947, "g"}, {948, "d"},
            {949, "e"}, {941, "e"}, {7952, "e"}, {7953, "e"}, {7954, "e"}, {7955, "e"},
            {7956, "e"}, {7957, "e"}, {8050, "e"}, {950, "z"}, {951, "i"}, {942, "i"},
            {7968, "i"}, {7969, "i"}, {7970, "i"}, {7971, "i"}, {7972, "i"}, {7973, "i"},
            {7974, "i"}, {7975, "i"}, {8080, "i"}, {8081, "i"}, {8082, "i"}, {8083, "i"},
            {8084, "i"}, {8085, "i"}, {8086, "i"}, {8087, "i"}, {8052, "i"}, {8130, "i"},
            {8131, "i"}, {8132, "i"}, {8134, "i"}, {8135, "i"}, {952, "t"}, {953, "i"},
            {943, "i"}, {970, "i"}, {912, "i"}, {7984, "i"}, {7985, "i"}, {7986, "i"},
            {7987, "i"}, {7988, "i"}, {7989, "i"}, {7990, "i"}, {7991, "i"}, {8054, "i"},
            {8144, "i"}, {8145, "i"}, {8146, "i"}, {8150, "i"}, {8151, "i"}, {954, "k"},
            {955, "l"}, {956, "m"}, {957, "n"}, {958, "k"}, {959, "o"}, {972, "o"},
            {8000, "o"}, {8001, "o"}, {8002, "o"}, {8003, "o"}, {8004, "o"}, {8005, "o"},
            {8056, "o"}, {960, "p"}, {961, "r"}, {8164, "r"}, {8165, "r"}, {963, "s"},
            {962, "s"}, {964, "t"}, {965, "y"}, {973, "y"}, {971, "y"}, {944, "y"},
            {8016, "y"}, {8017, "y"}, {8018, "y"}, {8019, "y"}, {8020, "y"}, {8021, "y"},
            {8022, "y"}, {8023, "y"}, {8058, "y"}, {8160, "y"}, {8161, "y"}, {8162, "y"},
            {8166, "y"}, {8167, "y"}, {966, "f"}, {967, "x"}, {968, "p"}, {969, "o"},
            {974, "o"}, {8032, "o"}, {8033, "o"}, {8034, "o"}, {8035, "o"}, {8036, "o"},
            {8037, "o"}, {8038, "o"}, {8039, "o"}, {8096, "o"}, {8097, "o"}, {8098, "o"},
            {8099, "o"}, {8100, "o"}, {8101, "o"}, {8102, "o"}, {8103, "o"}, {8060, "o"},
            {8178, "o"}, {8179, "o"}, {8180, "o"}, {8182, "o"}, {8183, "o"}, {1040, "A"},
            {1041, "B"}, {1042, "V"}, {1043, "G"}, {1044, "D"}, {1045, "E"}, {1025, "E"},
            {1046, "Z"}, {1047, "Z"}, {1048, "I"}, {1049, "I"}, {1050, "K"}, {1051, "L"},
            {1052, "M"}, {1053, "N"}, {1054, "O"}, {1055, "P"}, {1056, "R"}, {1057, "S"},
            {1058, "T"}, {1059, "U"}, {1060, "F"}, {1061, "K"}, {1062, "T"}, {1063, "C"},
            {1064, "S"}, {1065, "S"}, {1067, "Y"}, {1069, "E"}, {1070, "Y"}, {1071, "Y"},
            {1072, "A"}, {1073, "B"}, {1074, "V"}, {1075, "G"}, {1076, "D"}, {1077, "E"},
            {1105, "E"}, {1078, "Z"}, {1079, "Z"}, {1080, "I"}, {1081, "I"}, {1082, "K"},
            {1083, "L"}, {1084, "M"}, {1085, "N"}, {1086, "O"}, {1087, "P"}, {1088, "R"},
            {1089, "S"}, {1090, "T"}, {1091, "U"}, {1092, "F"}, {1093, "K"}, {1094, "T"},
            {1095, "C"}, {1096, "S"}, {1097, "S"}, {1099, "Y"}, {1101, "E"}, {1102, "Y"},
            {1103, "Y"}, {240, "d"}, {208, "D"}, {254, "t"}, {222, "T"}, {4304, "a"},
            {4305, "b"}, {4306, "g"}, {4307, "d"}, {4308, "e"}, {4309, "v"}, {4310, "z"},
            {4311, "t"}, {4312, "i"}, {4313, "k"}, {4314, "l"}, {4315, "m"}, {4316, "n"},
            {4317, "o"}, {4318, "p"}, {4319, "z"}, {4320, "r"}, {4321, "s"}, {4322, "t"},
            {4323, "u"}, {4324, "p"}, {4325, "k"}, {4326, "g"}, {4327, "q"}, {4328, "s"},
            {4329, "c"}, {4330, "t"}, {4331, "d"}, {4332, "t"}, {4333, "c"}, {4334, "k"},
            {4335, "j"}, {4336, "h"}
        };

        public static string Unaccent(string input, bool trimUnknown = true)
        {
            // Compose unicode points (i.e. e + ´ -> é)
            input = input.Normalize(NormalizationForm.FormC);
            var sb = new StringBuilder(input.Length);

            foreach (var c in input)
            {
                // First check if it's a benign character to save time
                if (c <= 127)
                {
                    sb.Append(c);
                }
                // Attempt to find an alternative ASCII representation for this character
                else if (TranslationTable.TryGetValue(c, out var replacement))
                {
                    // Replace with the ASCII equivalent (i.e. æ -> ae)
                    sb.Append(replacement);
                }
                else if (!trimUnknown)
                {
                    sb.Append('?');
                }
            }

            return sb.ToString();
        }

        public static string ProperNameCase(string stringToFormat)
        {
            CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
            TextInfo textInfo = cultureInfo.TextInfo;

            // Check if we have a string to format
            if (string.IsNullOrEmpty(stringToFormat))
            {
                return string.Empty;
            }

            // Check if string already contains both upper and lower, in which case assume it's correct
            bool hasLower = false;
            bool hasUpper = false;
            foreach (char c in stringToFormat)
            {
                if (!char.IsLetter(c))
                    continue;

                if (char.IsUpper(c))
                    hasUpper = true;
                else
                    hasLower = true;

                if (hasUpper && hasLower)
                    return stringToFormat;
            }

            // From http://msdn.microsoft.com/en-us/library/system.globalization.textinfo.totitlecase.aspx:
            // "However, this method does not currently provide proper casing to convert a word that is entirely uppercase, such as an acronym."
            return textInfo.ToTitleCase(stringToFormat.ToLower().Trim());
        }

        static private char[] TrimChars = new [] { ' ', '\t', '\v', ',', '.', ';' };
        [GeneratedRegex(@"\s{2,}")]
        static private partial Regex RepeatedWhitespaceRegex();
        static private string TrimName(string name)
        {
            name = RepeatedWhitespaceRegex().Replace(name, " ");
            return name.Trim(TrimChars);
        }

        [GeneratedRegex(@"(^[M|D]rs?\.? ?)|\b(jr|sr|[xiv]+|m\.?d\.?|d\.?d\.?s\.?)\b|,.*$", RegexOptions.IgnoreCase)]
        static private partial Regex SalutationRegex();
        static public (string FirstName, string LastName) SplitName(string name, bool removeSalutations = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = string.Empty;
            }

            name = TrimName(name);
            name = removeSalutations ? SalutationRegex().Replace(name, "") : name;
            int lastSpace = name.LastIndexOf(' ');

            var firstName = TrimName(lastSpace > 0 ? name.Substring(0, lastSpace) : name);
            var lastName = TrimName(lastSpace > 0 ? name.Substring(lastSpace + 1) : string.Empty);

            return (firstName, lastName);
        }

        static public (string FirstName, string LastName) SanitizeName(string first, string last)
        {
            first = ProperNameCase(RemoveSalutation(first));
            last = ProperNameCase(RemoveSalutation(last));

            //We have too many users that put "First Last" as first name and "Last" as last name
            string match = $" {last.ToLowerInvariant()}";
            if (first.ToLowerInvariant().EndsWith(match))
            {
                first = first.Substring(0, first.Length - match.Length).TrimEnd();
            }

            return (TrimName(first), TrimName(last));
        }

        static public string RemoveSalutation(string name)
        {
            name = SalutationRegex().Replace(name, "");
            return name;
        }
    }
}

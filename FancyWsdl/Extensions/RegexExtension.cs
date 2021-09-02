using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FancyWsdl.Extensions
{
    public static class RegexExtension
    {
        public static IList<Match> AsList(this MatchCollection src)
        {
            return src.Cast<Match>().ToList();
        }
    }
}

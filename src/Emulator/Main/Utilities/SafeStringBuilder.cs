//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Text;

namespace Antmicro.Renode.Utilities
{
    public class SafeStringBuilder
    {
        public SafeStringBuilder()
        {
            buffer = new StringBuilder();
        }

        public bool TryDump(out string content)
        {
            lock(buffer)
            {
                if(buffer.Length == 0)
                {
                    content = null;
                    return false;
                }

                content = buffer.ToString();
                return true;
            }
        }

        public override string ToString()
        {
            lock(buffer)
            {
                return buffer.ToString();
            }
        }

        public string Unload()
        {
            lock(buffer)
            {
                var content = buffer.ToString();
                buffer.Clear();
                return content;
            }
        }

        public void Append(string str)
        {
            lock(buffer)
            {
                buffer.Append(str);
            }
        }

        public void Append(char str)
        {
            lock(buffer)
            {
                buffer.Append(str);
            }
        }

        public void AppendFormat(string str, params object[] args)
        {
            lock(buffer)
            {
                buffer.AppendFormat(str, args);
            }
        }

        public void AppendLine(string str)
        {
            lock(buffer)
            {
                buffer.AppendLine(str);
            }
        }

        private readonly StringBuilder buffer;
    }
}


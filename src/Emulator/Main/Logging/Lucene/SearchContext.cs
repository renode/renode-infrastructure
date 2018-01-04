//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Logging.Lucene
{
    public class SearchContext
    {
        public SearchContext(int count)
        {
            ResultsCount = count;
            CurrentResult = 1;
        }

        public void Advance(Direction direction)
        {
            CurrentResult += (direction == Direction.Backward ? 1 : -1);
        }

        public ulong? PreviousResultId { get; set; }
        public int CurrentResult { get; private set; }
        public int ResultsCount { get; private set; }
    }
}


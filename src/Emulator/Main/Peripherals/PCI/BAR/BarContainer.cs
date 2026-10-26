//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.PCI;

public class BarContainer : IEnumerable<BarContainer.TargetBar>
{
    public BarContainer()
    {
        comparer = new AddressStartComparer();
        memoryMap = new SortedList<Range, TargetBar>(comparer);
    }

    public IEnumerator<TargetBar> GetEnumerator()
    {
        return memoryMap.Values.GetEnumerator();
    }

    public bool TryRegisterBar(Range range, IPCIePeripheral peripheral, uint bar)
    {
        // Remove existing registration (it will be re-added later at a different offset)
        // This path will always be hit at least once during the bus enumeration, because of how BAR detection and sizing works
        // By writing all ones to check supported BAR size
        var previousRegistration = memoryMap.Where(x => x.Value.BarNumber == bar && x.Value.TargetPeripheral == peripheral).Select(x => x.Key);
        if(previousRegistration.Any())
        {
            memoryMap.Remove(previousRegistration.SingleOrDefault());
        }

        // Find overlapping range
        var overlappingBar = memoryMap.Where(bar => bar.Key.Intersects(range));
        if(overlappingBar.Any())
        {
            // Cannot register an overlapping BAR - this usually means a bugged HW
            // This path will also be hit during bus discovery, where SW checks BAR sizes
            return false;
        }

        memoryMap[range] = new TargetBar { BarNumber = bar, TargetPeripheral = peripheral, BarRange = range };
        return true;
    }

    public bool TryFindBar(ulong translatedAddress, out TargetBar bar)
    {
        bar = default(TargetBar);
        var idx = FindFirstGreaterOrEqualElementsIndex(memoryMap, e => comparer.Compare(e, new Range(translatedAddress, 1)));
        if(idx == -1)
        {
            // Falls below the first range
            return false;
        }
        var firstElement = memoryMap.GetValueAtIndex(idx);
        if(firstElement.BarRange.Contains(translatedAddress))
        {
            // Either exact match, or the first below
            bar = firstElement;
            return true;
        }
        else
        {
            // No match, the address is above the range
            return false;
        }
    }

    /// <summary>
    /// This function will find the lowest index that the element is greater or equal to.
    /// </summary>
    /// <returns>The lowest or exact index or -1 if the element is below any items of the list</returns>
    private int FindFirstGreaterOrEqualElementsIndex<T, V>(SortedList<T, V> list, Func<T, int> compFunc)
    {
        int left = 0;
        int right = list.Count - 1;
        while(left <= right)
        {
            int pivot = (left + right) / 2;

            var element = list.GetKeyAtIndex(pivot);
            var compResult = compFunc(element);

            if(compResult < 0)
            {
                left = pivot + 1;
            }
            else if(compResult > 0)
            {
                right = pivot - 1;
            }
            else
            {
                return pivot;
            }
        }
        return Math.Min(left, right);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private readonly SortedList<Range, TargetBar> memoryMap;
    private readonly AddressStartComparer comparer;

    public struct TargetBar
    {
        public IPCIePeripheral TargetPeripheral;
        public uint BarNumber;
        public Range BarRange;

        public override string ToString()
        {
            return $"BAR_{BarNumber} @ {BarRange.ToString()}, for {TargetPeripheral.GetName()}";
        }
    }

    private class AddressStartComparer : IComparer<Range>
    {
        public int Compare(Range x, Range y)
        {
            if(x.StartAddress < y.StartAddress)
            {
                return -1;
            }
            else if(x.StartAddress == y.StartAddress)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }
    }
}

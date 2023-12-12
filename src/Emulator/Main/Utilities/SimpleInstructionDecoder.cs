//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities
{
    public class SimpleInstructionDecoder<TInstruction>
    {
        /// <remarks>
        /// This decoder assumes that the opcode is at most 1 byte long. It will parse the opcode, starting from the opcode's MSB (Most Significant Bit).
        /// </remarks>
        public void AddOpcode(byte value, int opcodeLength, Func<TInstruction> newInstruction, int bitNumber = 7)
        {
            if(opcodeLength > 8)
            {
                throw new ArgumentException($"The opcode cannot be longer than 8 bits: is {opcodeLength} bits long");
            }
            if(bitNumber < 8 - opcodeLength)
            {
                // We're done parsing - we traversed the whole opcode length to get here
                // so we encoded the path to our instruction in the tree - this is the leaf node (the last in the tree, without any children)
                if(this.instruction != null)
                {
                    // There already exists an instruction here - so throw exception, not to override it by the new one
                    throw new InvalidOperationException($"Duplicate instruction registered: {value} already exists");
                }
                this.instruction = newInstruction;
            }
            else
            {
                // If there is an instruction already here, and we haven't parsed the whole length
                // this means we entered an invalid state, where some instruction have different lengths than others
                // in such a way, they override the longer ones in the tree (e.g. instruction 0b11 with length 2 would silently override 0b1101 with length 4, if we parsed from MSB).
                // If we didn't abort here, the longer instruction wouldn't be parsed at all in `TryParseOpcode` and replaced with the shorter one each time!
                if(instruction != null)
                {
                    throw new InvalidOperationException($"Cannot register instruction: {value} the lengths and contents will clash with instruction: {instruction}, which will prevent correct parsing");
                }
                // Construct a binary tree here, if we haven't already
                if(children == null)
                {
                    children = new SimpleInstructionDecoder<TInstruction>[2];
                    children[0] = new SimpleInstructionDecoder<TInstruction>();
                    children[1] = new SimpleInstructionDecoder<TInstruction>();
                }
                var nextBit = BitHelper.IsBitSet(value, (byte)bitNumber);
                bitNumber--;
                // Depending whether the next bit of the opcode is 0 or 1, choose the path in the binary tree
                // and recursively call `AddOpcode` on the new instance of the Decoder,
                // that represents a child node in the tree
                children[nextBit ? 1 : 0].AddOpcode(value, opcodeLength, newInstruction, bitNumber);
            }
        }

        public bool TryParseOpcode(byte value, out TInstruction result, byte bitNumber = 7)
        {
            if(instruction != null)
            {
                // There is an instruction here - it's the one we are looking for.
                // The instructions are added only in leaf node of the tree - there is a check to enforce that.
                // We end parsing right now with success.
                result = instruction();
                return true;
            }
            if(children == null)
            {
                // If there are no further children, and we didn't already find the instruction
                // that means that it doesn't exist in our tree
                result = default(TInstruction);
                return false;
            }
            var nextBit = BitHelper.IsBitSet(value, bitNumber);
            bitNumber--;
            // To find the instruction, we have to traverse the binary tree, 
            // constructed when we called `AddOpcode` previously
            return children[nextBit ? 1 : 0].TryParseOpcode(value, out result, bitNumber);
        }

        // Binary tree, since opcodes differ by 0/1 on each bit position
        private SimpleInstructionDecoder<TInstruction>[] children;
        private Func<TInstruction> instruction;
    }
}

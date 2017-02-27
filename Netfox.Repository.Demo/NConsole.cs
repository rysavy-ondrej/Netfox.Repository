using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netfox.Repository.Demo
{
    public static class NConsole
    {

        public static void Init(int width, int height)
        {
            Console.WindowHeight = Math.Min(height, Console.BufferHeight);
            Console.WindowWidth = Math.Min(width, Console.BufferWidth);
            Console.SetBufferSize(width, height);
            Console.SetWindowSize(width, height);
            Console.SetCursorPosition(0, 0);
        }

        private static readonly ReaderWriterLockSlim consoleLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public static void Clear()
        {
            consoleLock.EnterWriteLock();
            Console.Clear();
            consoleLock.ExitWriteLock();
        }


        public static void WriteHline()
        {
            consoleLock.EnterWriteLock();
            {
                Console.Write(new StringBuilder().Append('-', Console.BufferWidth));
            }
            consoleLock.ExitWriteLock();
        }
        public static void WriteLine(string format, params object [] param)
        {
            consoleLock.EnterWriteLock();
            {
                Console.WriteLine(format, param);
            }
            consoleLock.ExitWriteLock();
        }
        public static void WriteLine(object obj)
        {
            consoleLock.EnterWriteLock();
            {
                Console.WriteLine(obj);
            }
            consoleLock.ExitWriteLock();
        }
        public static void Write(string format, params object[] param)
        {
            consoleLock.EnterWriteLock();
            {
                Console.Write(format, param);
            }
            consoleLock.ExitWriteLock();
        }

        public static void Write(string text)
        {
            consoleLock.EnterWriteLock();
            {
                Console.Write(text);
            }
            consoleLock.ExitWriteLock();
        }

        static readonly Stack<Tuple<int, int>> _positionStack = new Stack<Tuple<int, int>>(); 
        public static void PushPosition()
        {
            consoleLock.EnterWriteLock();
            _positionStack.Push(new Tuple<int,int>(Console.CursorLeft, Console.CursorTop));
            consoleLock.ExitWriteLock();
        }

        public static void PopPosition()
        {
            consoleLock.EnterWriteLock();
            var pos = _positionStack.Pop();
            Console.SetCursorPosition(pos.Item1,pos.Item2);
            consoleLock.ExitWriteLock();
        }

        public static void Rectangle(int left, int top, int width, int height)
        {
            consoleLock.EnterWriteLock();
            var origRow = Console.CursorTop;
            var origCol = Console.CursorLeft;


            var sbEdge = new StringBuilder().Append('+').Append('-', width).Append('+').ToString();
            var sbLine = new StringBuilder().Append('|').Append(' ', width).Append('|').ToString();
            Console.SetCursorPosition(left, top);
            Console.Write(sbEdge);
            // draw left and right edges and clear the space 
            for (int i = top + 1; i < top + height; i++)
            {
                Console.SetCursorPosition(left, i);
                Console.Write(sbLine);
            }
            Console.SetCursorPosition(left, top + height);
            Console.Write(sbEdge);

            Console.SetCursorPosition(origCol, origRow);

            consoleLock.ExitWriteLock();
        }

        public static void EnterLock()
        {
            consoleLock.EnterWriteLock();
        }

        public static void ExitLock()
        {
            consoleLock.ExitWriteLock();
        }

        public static void SetCursorPosition(int x, int y)
        {
            consoleLock.EnterWriteLock();
            Console.SetCursorPosition(x,y);
            consoleLock.ExitWriteLock();
        }
    }
}

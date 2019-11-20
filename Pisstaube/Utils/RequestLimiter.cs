using System;
using System.Threading;

namespace Pisstaube.Utils
{
    public class RequestLimiter
    {
        private readonly int _requestAmount;
        private readonly TimeSpan _howLong;
        private DateTime _dt;
        private DateTime _dtNext;

        private int _req;

        public RequestLimiter(int requestAmount, TimeSpan howLong)
        {
            _requestAmount = requestAmount - 1;
            _howLong = howLong;
            _dt = DateTime.Now;
            _dtNext = DateTime.Now;

            _dtNext += howLong;
        }

        public void Limit()
        {
            if (_dt > _dtNext)
            {
                _req = 1;

                _dtNext = DateTime.Now;
                _dtNext += _howLong;
            }

            if (_req > _requestAmount)
                if (_dtNext.Subtract(_dt).TotalMilliseconds > 0)
                {
                    Console.WriteLine(_dtNext.Subtract(_dt).TotalMilliseconds);
                    Thread.Sleep(_dtNext.Subtract(_dt));
                }

            _req++;
            _dt = DateTime.Now;
        }
    }
}
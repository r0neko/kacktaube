using System;
using System.Threading;

namespace Pisstaube.Utils
{
    /// <summary>
    /// a Request Limiter to prevent overflowing osu!API V2 with tons of API Requests.
    /// </summary>
    public class RequestLimiter
    {
        private readonly int requestAmount;
        private readonly TimeSpan howLong;
        private DateTime dtLast;
        private DateTime dtNext;

        private int req;

        public RequestLimiter(int requestAmount, TimeSpan howLong)
        {
            this.requestAmount = requestAmount - 1;
            this.howLong = howLong;
            dtLast = DateTime.Now;
            dtNext = DateTime.Now;

            dtNext += howLong;
        }

        /// <summary>
        /// Limits the current Request Count (freezes Thread until the Timeout is over)
        /// </summary>
        public void Limit()
        {
            if (dtLast > dtNext)
            {
                req = 1;

                dtNext = DateTime.Now;
                dtNext += howLong;
            }

            if (req > requestAmount && dtNext.Subtract(dtLast).TotalMilliseconds > 0)
                Thread.Sleep(dtNext.Subtract(dtLast));

            req++;
            dtLast = DateTime.Now;
        }
    }
}
using System;
using System.Threading.Tasks;

namespace FG.CheckoutAndBuild2.Common
{
    public static class Waiter
    {
        public static async Task WaitForAsync(Func<bool> expression)
        {
            while (!expression())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20));
            }
        }
    }
}
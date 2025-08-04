
namespace duo_code.Utils
{
    public static class GracefulShutdownHandler
    {
        private static int _ctrlCPressCount = 0;
        
        public static CancellationTokenSource Setup()
        {
            var cts = new CancellationTokenSource();
            
            Console.CancelKeyPress += (sender, e) =>
            {
                _ctrlCPressCount++;
                
                if (_ctrlCPressCount == 1)
                {
                    e.Cancel = true;
                    try
                    {
                        if (!cts.Token.IsCancellationRequested)
                        {
                            cts.Cancel();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                    WriteLine("\nGracefully shutting down... Press Ctrl+C again to force exit.");
                }
                else
                {
                    WriteLine("\nForce exit requested.");
                    Environment.Exit(1);
                }
            };
            
            return cts;
        }
    }
}
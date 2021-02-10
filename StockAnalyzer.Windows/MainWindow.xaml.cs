using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;

namespace StockAnalyzer.Windows
{
    public partial class MainWindow : Window
    {
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        static Object obj = new Object();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            tokenSource = new CancellationTokenSource();
            tokenSource.Token.Register(() =>
            {
                Notes.Text += $"\n Cancellation is requested ";
            });

            #region Before loading stock data
            var watch = new Stopwatch();
            watch.Start();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = true;
            #endregion

            //you can use Async&Await OR Task parallel library

            #region [Async & await]
            #region [using .wait() or .result]
            /*
             * will block UI thread 
             */
            //Task.Delay(10000).Wait();
            #endregion

            //await Task.Delay(10000);
            await GetData();
            #endregion

            #region [TPL]
            /*
             * any method doing a non cpu operation should be marked using await inside async operation
             * and when u call this method you should await them
             * don't forget ever to return task from async method
             */
            var t1 = Task.Run(() => { throw new Exception(); Thread.Sleep(10000); });
            /* code inside continue with won't be executed untill it
             * is done and it will be skipped and code beneth it willl be executed
             */
            t1.ContinueWith((s) => {
                int t = 1;

            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            t1.ContinueWith((s) => {
                int t = 1;

            }, TaskContinuationOptions.OnlyOnFaulted);

            #endregion

            #region [Canelation token]
            var t2 = Task.Run(()=> {
                long x = 1;
                while(x<10000000000)
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        return x;
                    }
                    x++;
                }
                return x;
            }, tokenSource.Token);

            #endregion
           
            var data = new List<StockPrice>();

            #region [Cancelation token]
            t2.ContinueWith((o) => {
                var x2 = 1;
            },
           tokenSource.Token,
           TaskContinuationOptions.OnlyOnRanToCompletion,
           TaskScheduler.Current
           );

            t2.ContinueWith((o) => {
                var x2 = 1;
                Dispatcher.Invoke(() => {
                    Stocks.ItemsSource = data;

                    #region After stock data is loaded
                    StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
                    StockProgress.Visibility = Visibility.Hidden;
                    #endregion
                });
            });
            #endregion

            #region [WhenAll & WhenAny]
            var tasks = new List<Task<List<int>>>();
            for(int i=0;i<3;i++)
            {
                tasks.Add(GetNumbers());
            }
            var result = await Task.WhenAll(tasks);
            var res = result.SelectMany(lst=>lst).ToArray();

            var str ="";
            Array.ForEach(res, _e => { str+= $"{_e} , "; });
            Notes.Text = str;



            //This code is repeated to test task.WhenAny

            tasks = new List<Task<List<int>>>();
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(GetNumbers());
            }
            var timer = Task.Delay(50);

            var _result = await Task.WhenAny(tasks[0],timer);
            if(_result == timer)
            {
                Notes.Text += "\n Too much time is required";
            }

            #endregion

            #region [ConcurrentBag => is a thread safe lst && Threadsafe - int , long , decimal]
            var apiResuls = new ConcurrentBag<int>();
            int threadSafeIntORLong = 0;  
            long threadSafeIntORLong2 = 0;
            decimal threadSafeD = 0m;

            /* ConcurrentBag, Interlocked, Lock(new Object()) => ensure that only one thread 
             * works on them at a time
            */
            var tasks3 = new List<Task<List<int>>>();
            var tg = "";

            for (int i=0;i<3;i++)
            {
              GetNumbers().ContinueWith((r)=> {
                  Array.ForEach(r.Result.ToArray(), (es) => { apiResuls.Add(es); });
                  Interlocked.Add(ref threadSafeIntORLong,100);
                  Interlocked.Add(ref threadSafeIntORLong2, 100);
                  lock(obj)
                  {
                      threadSafeD += 10.5m;
                  }
                  tg = String.Join(",", apiResuls.ToArray());
                  Dispatcher.Invoke(()=> { Notes.Text += $"\n ConcurrentBag Data : {tg}"; });
                  Dispatcher.Invoke(()=> { Notes.Text += $"\n Interlocked - int : {threadSafeIntORLong}"; });
                  Dispatcher.Invoke(()=> { Notes.Text += $"\n Interlocked - long : {threadSafeIntORLong2}"; });
                  Dispatcher.Invoke(()=> { Notes.Text += $"\n Lock - decimal : {threadSafeD}"; });
              });
            }

            #endregion

            //Parallel extension is built on TPL
            /*Important => Parallel extension methods blocks their threads untill
             * their operations are done. You will notice this in the next example not respond
             * in this positions a,b,c
             */

            #region [Parallel Extensions - Invoke]
            var _watch = new Stopwatch();
            _watch.Start();
            var _t1 = Task.Delay(500);
            var _t2 = Task.Delay(500);
            var _t3 = Task.Delay(500);
            await Task.WhenAll(_t1,_t2,_t3); // =====>a
            _watch.Stop();
            Notes.Text += $"\n w/o Parallel Extensions - Invoke :: {_watch.Elapsed}";
           
            _watch.Reset();
            _watch.Start();
            Parallel.Invoke( // =====>b
              () => { Thread.Sleep(500); },  
              () => { Thread.Sleep(500); },  
              () => { Thread.Sleep(500); }
            );
            _watch.Stop();
            Notes.Text += $"\n with Parallel Extensions - Invoke :: {_watch.Elapsed}";

            _watch.Reset();
            _watch.Start();

            //MaxDegreeOfParallelism =1 means that only one thread will work on these tasks
            //MaxDegreeOfParallelism =2 means that two threads will work on these tasks

            Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism= 2}, // =====>c
              () => { Thread.Sleep(500); },
              () => { Thread.Sleep(500); },
              () => { Thread.Sleep(500); }
            );
            _watch.Stop();
            Notes.Text += $"\n with Parallel Extensions \"MaxDegreeOfParallelism\" = 2- Invoke :: {_watch.Elapsed}";

            #endregion

            #region [Parallel Extensions - FoEach]
            var numbers = new List<int> { 1,2,3};
            var numbersRes = new ConcurrentBag<int>();

            _watch.Reset();
            _watch.Start();
            Array.ForEach(numbers.ToArray(),(n)=> 
            {
                Thread.Sleep(500);
                numbersRes.Add(n * 2);
            });
            _watch.Stop();
            Notes.Text += $"\n \n w/o Parallel Extensions - FoEach :: {_watch.Elapsed}";


            numbersRes = new ConcurrentBag<int>();

            _watch.Reset();
            _watch.Start();
            Parallel.ForEach(numbers, (n) =>
            {
                Thread.Sleep(500);
                numbersRes.Add(n * 2);
            });
            _watch.Stop();
            Notes.Text += $"\n with Parallel Extensions - FoEach :: {_watch.Elapsed}";

            #endregion

        }


        public async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            tokenSource.Cancel();
        }

        public async Task GetData()
        {
            //throw new Exception();
            await Task.Delay(100);
        }

        public async Task<List<int>> GetNumbers()
        {
            return await Task.Run(()=>
            {
                Thread.Sleep(50);
                return new List<int> { 1,2,3};
            });

        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

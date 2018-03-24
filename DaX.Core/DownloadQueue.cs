﻿using Fiddler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DaX
{
    public class DownloadQueueProcessor
    {
        private string dax_id;
        int tot = 0;
        int comp = 0;
        //private int MaxParallel;
        Session Session;
        //public List<Tuple<int, int>> Ranges { get; set; } = new List<Tuple<int, int>>();
        public void Initialize(Session dsession)
        {
            Session = dsession;
            //MaxParallel = parallelcount;
            int rangeLower = 0;
            int incre = 1;

            dax_id = Guid.NewGuid().ToString();
            int rangeDelta = Session.Size / 500;
            //List<string> ranges = new List<string>();
            //List<int> deltas = new List<int>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                dsession.DownloadQueue.Clear();
            });
            while (((rangeLower + (rangeDelta * incre)) < Session.Size))
            {
                var rangeUpper = (rangeLower + (rangeDelta * incre));
                Application.Current.Dispatcher.Invoke(() =>
                {
                    dsession.DownloadQueue.Add(new DownloadQueueItem()
                    {
                        RangeStart = rangeLower,
                        RangeEnd = rangeUpper,
                    });
                });
                rangeLower += (rangeDelta * incre) + 1;
                //deltas.Add((rangeDelta * incre));
                incre++;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                dsession.DownloadQueue.Add(new DownloadQueueItem()
                {
                    RangeStart = rangeLower,
                    RangeEnd = Session.Size
                });
            });
        }

        public void Start(int howmany)
        {
            var rangestostart = Session.DownloadQueue.Where(y => y.Processed==false).Take(howmany);
            //foreach (var rangetoremove in rangestostart)
            //{
            //    Ranges.Remove(rangetoremove);
            //}
            var oS = Session.fSession;
            foreach (var r in rangestostart)
            {
                r.Processed = null;
                var requestHeaders = oS.RequestHeaders.Clone() as HTTPRequestHeaders;
                requestHeaders["Range"] = "bytes=" + r.RangeStart + " - " + r.RangeEnd; //+ ranges[r];
                var newflags = new System.Collections.Specialized.StringDictionary { { "dax_id", dax_id } };

                Interlocked.Increment(ref tot);
                //tot++;
                r.Session = FiddlerApplication.oProxy.SendRequest(requestHeaders,
                     oS.RequestBody,
                     newflags,
                     (sender, evtstatechangeargs) =>
                     {
                         if (evtstatechangeargs.newState == SessionStates.Done)
                         {
                             r.Processed = true;
                             Interlocked.Increment(ref comp);
                             Start(1);
                             //comp++;
                             Session.Progress = Convert.ToInt32((comp * 100.0) / (tot));
                             foreach (var session in Session.DownloadQueue)
                             {
                                 if (session.Session.state != SessionStates.Done)
                                 {
                                     return;
                                 }
                             }
                             if (Session.Progress != 100) return;
                             Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "DaXDL"));

                             var idfiles = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "DaXCaps"), Path.GetFileName(dax_id) + "_DaX_" + "*" + "_XaD_" + "*");
                             var destname = idfiles[0];
                             destname = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "DaXDL", destname.Substring(destname.IndexOf("_XaD_") + "_XaD_".Length));

                             using (Stream destStream = File.OpenWrite(destname))
                             {
                                 foreach (string srcFileName in idfiles.OrderBy(x => x, new AlphanumComparatorFast()))
                                 {
                                     try
                                     {
                                         using (Stream srcStream = File.OpenRead(srcFileName))
                                         {
                                             srcStream.CopyTo(destStream);
                                         }
                                         File.Delete(srcFileName);
                                     }
                                     catch (Exception ex)
                                     {
                                         Console.WriteLine(ex);
                                     }
                                 }
                             }
                             Application.Current.Dispatcher.Invoke(() =>
                             {
                                 //Session.SubSessions.Clear();// = new ConcurrentBag<Fiddler.Session>();
                             });
                             GC.Collect();
                         }
                     });

            }
        }
    }
}
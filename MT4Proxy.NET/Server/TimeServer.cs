﻿using MT4Proxy.NET.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace MT4Proxy.NET
{
    class TimeServer : ConfigBase, IServer
    {
        private static int Period
        {
            get;
            set;
        }
        internal override void LoadConfig(NLog.Internal.ConfigurationManager aConfig)
        {
            Period = int.Parse(aConfig.AppSettings["time_check_period_seconds"]);
        }

        private static Thread _thTime = null;

        public void Initialize()
        {
            Work();
            _thTime = new Thread(() => 
            {
                while (true)
                {
                    Thread.Sleep(Period * 1000);
                    try
                    {
                        Work();
                    }
                    catch
                    { }
                }
            });
            _thTime.Start();
        }

        private static void Work()
        {
            var logger = Utils.CommonLog;
            var lstDelay = new List<double>();
            var lstOffset = new List<double>();
            var api = Poll.New();
            logger.Info("时间校正服务正在开始");
            for (int i = 0; i < 15; i++)
            {
                var T1 = DateTime.UtcNow;
                var T2T3 = api.ServerTime().FromTime32();
                var T4 = DateTime.UtcNow;
                var delay = (T4 - T1).TotalMilliseconds;
                var offset = ((T2T3 - T1) + (T2T3 - T4)).TotalMilliseconds / 2;
                lstDelay.Add(delay);
                lstOffset.Add(offset);
                logger.Info("时间比对第{0}次，偏移{1}小时，延迟{2}毫秒",
                    i + 1, offset / 1000 / 3600, delay);
            }
            lstDelay.Remove(lstDelay.Min());
            lstDelay.Remove(lstDelay.Max());
            lstOffset.Remove(lstOffset.Min());
            lstOffset.Remove(lstOffset.Max());
            var avgDelay = lstDelay.Average();
            var avgOffset = lstOffset.Average();
            var varianceDelay = lstDelay.Sum(i => Math.Pow(i - avgDelay, 2));
            logger.Info("时间平均偏移{0}小时，延迟{1}毫秒",
                    avgOffset / 1000 / 3600, avgDelay);
            if (avgDelay > 20)
                logger.Warn("警告，系统间延迟过高");
            if (varianceDelay > 8)
                logger.Warn("警告，系统间网络不稳定");
            _offset = TimeSpan.FromMilliseconds(avgOffset + avgDelay);
            logger.Info("时间校正服务已经完成");
        }

        public void Stop()
        {
            if(_thTime!=null)
            {
                _thTime.Abort();
                _thTime = null;
            }
            ServerContainer.FinishStop();
        }

        private static TimeSpan _offset;

        /// <summary>
        /// 获取MT4此刻时间
        /// </summary>
        public static DateTime Now
        {
            get
            {
                return DateTime.UtcNow + _offset;
            }
        }
    }
}

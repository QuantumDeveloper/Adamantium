using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Game.Core;
using Adamantium.UI;
using Adamantium.UI.Controls;
using AdamantiumVulkan.Core;
using Serilog;

namespace Adamantium.Game;

public abstract class GameApplication : UIApplication
    {
        public IGameService GameService { get; private set; }
        
        public GameApplication()
        {
            
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            GameService = new GameService();
            EntityWorld.ServiceManager.OnDrawStarted += ServiceManagerOnDrawStarted;
            EntityWorld.ServiceManager.OnDrawFinished += ServiceManagerOnOnDrawFinished;
            GameService.OnGameAdded += GameServiceOnGameAdded;
        }

        private void GameServiceOnGameAdded(IGame obj)
        {
            
        }

        private void ServiceManagerOnDrawStarted(IRenderService service, AppTime time)
        {
            GameService.RunGames(service, time);
        }
        
        private void ServiceManagerOnOnDrawFinished(IRenderService arg1, AppTime arg2)
        {
           GameService.CopyOutput(arg1.GraphicsDevice);
        }

        protected override void RegisterServices(IContainerRegistry containerRegistry)
        {
            base.RegisterServices(containerRegistry);
            containerRegistry.RegisterSingleton<IGameService>(GameService);
        }

        protected override void OnBeforeEndScene()
        {
            
        }

        // protected override void Submit()
        // {
        //     base.Submit();
        //     var graphicQueue = GraphicsDeviceService.MainGraphicsDevice.GetAvailableGraphicsQueue();
        //     var submitInfos = new List<SubmitInfo>();
        //
        //     foreach (var device in GraphicsDeviceService.MainGraphicsDevice.GraphicsDevices)
        //     {
        //         var submitInfo = device.PrepareSubmit();
        //         if (submitInfo != null)
        //         {
        //             submitInfos.Add(submitInfo);
        //         }
        //     }
        //
        //     submitInfos.Reverse();
        //
        //     if (submitInfos.Count > 0)
        //     {
        //         GraphicsDeviceService.MainGraphicsDevice.Submit(graphicQueue, submitInfos.ToArray());
        //     }
        //     
        //     GraphicsDeviceService.MainGraphicsDevice.OnFrameFinished();
        // }
    }
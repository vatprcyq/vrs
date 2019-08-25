﻿// Copyright © 2010 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirtualRadar.Interface.View;
using VirtualRadar.Interface.Presenter;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Settings;

namespace VirtualRadar.Library.Presenter
{
    /// <summary>
    /// The default implementation of <see cref="IAboutPresenter"/>.
    /// </summary>
    class AboutPresenter : IAboutPresenter
    {
        /// <summary>
        /// The view that the presenter is controlling.
        /// </summary>
        private IAboutView _View;

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="view"></param>
        public void Initialise(IAboutView view)
        {
            _View = view;
            _View.OpenConfigurationFolderClicked += View_OpenConfigurationFolderClicked;

            var applicationInformation = Factory.Resolve<IApplicationInformation>();
            var configurationStorage = Factory.Resolve<IConfigurationStorage>().Singleton;
            var runtimeEnvironment = Factory.Resolve<IRuntimeEnvironment>().Singleton;

            _View.BuildDate = applicationInformation.BuildDate;
            _View.Caption = applicationInformation.ApplicationName;
            _View.ConfigurationFolder = configurationStorage.Folder;
            _View.Copyright = applicationInformation.Copyright;
            _View.Description = applicationInformation.Description;
            _View.Is64BitProcess = runtimeEnvironment.Is64BitProcess;
            _View.IsMono = runtimeEnvironment.IsMono;
            _View.ProductName = applicationInformation.ProductName;
            _View.Version = applicationInformation.FullVersion;
        }

        /// <summary>
        /// Raised when the view decides that the user wants to see the content of the configuration folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_OpenConfigurationFolderClicked(object sender, EventArgs args)
        {
            _View.ShowConfigurationFolderContents();
        }
    }
}

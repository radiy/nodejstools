﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NodejsTools.Npm.SPI{
    internal class NpmCommander : INpmCommander{
        private NpmController _npmController;
        private NpmCommand _command;
        private bool _disposed;

        public NpmCommander(NpmController controller){
            _npmController = controller;
            OutputLogged += _npmController.LogOutput;
            ErrorLogged += _npmController.LogError;
            ExceptionLogged += _npmController.LogException;
        }

        public void Dispose(){
            if (!_disposed){
                _disposed = true;
                OutputLogged -= _npmController.LogOutput;
                ErrorLogged -= _npmController.LogError;
                ExceptionLogged -= _npmController.LogException;
            }
        }

        private void FireNpmLogEvent(string logText, EventHandler<NpmLogEventArgs> handlers){
            if (null != handlers && !string.IsNullOrEmpty(logText)){
                handlers(this, new NpmLogEventArgs(logText));
            }
        }

        public event EventHandler<NpmLogEventArgs> OutputLogged;

        private void OnOutputLogged(string logText){
            FireNpmLogEvent(logText, OutputLogged);
        }

        public event EventHandler<NpmLogEventArgs> ErrorLogged;

        private void OnErrorLogged(string logText){
            FireNpmLogEvent(logText, ErrorLogged);
        }

        public event EventHandler<NpmExceptionEventArgs> ExceptionLogged;

        private void OnExceptionLogged(Exception e){
            var handlers = ExceptionLogged;
            if (null != handlers){
                handlers(this, new NpmExceptionEventArgs(e));
            }
        }

        public void CancelCurrentCommand(){
            if (null != _command){
                _command.CancelCurrentTask();
            }
        }

        //  TODO: events should be fired as data is logged, not in one massive barf at the end
        private void FireLogEvents(NpmCommand command){
            //  Filter this out because we ony using search to return the entire module catalogue,
            //  which will spew 47,000+ lines of total guff that the user probably isn't interested
            //  in to the npm log in the output window.
            if (command is NpmSearchCommand){
                return;
            }
            OnOutputLogged(command.StandardOutput);
            OnErrorLogged(command.StandardError);
        }

        private async Task<bool> InstallPackageByVersionAsync(
            string packageName,
            string versionRange,
            DependencyType type,
            bool global){
            try{
                _command = new NpmInstallCommand(
                    _npmController.FullPathToRootPackageDirectory,
                    packageName,
                    versionRange,
                    type,
                    global,
                    _npmController.PathToNpm,
                    _npmController.UseFallbackIfNpmNotFound);

                var retVal = await _command.ExecuteAsync();
                FireLogEvents(_command);
                _npmController.Refresh();
                return retVal;
            } catch (Exception e){
                OnExceptionLogged(e);
                return false;
            }
        }

        public async Task<bool> InstallPackageByVersionAsync(
            string packageName,
            string versionRange,
            DependencyType type){
            return await InstallPackageByVersionAsync(packageName, versionRange, type, false);
        }

        public async Task<bool> InstallGlobalPackageByVersionAsync(string packageName, string versionRange){
            return await InstallPackageByVersionAsync(packageName, versionRange, DependencyType.Standard, true);
        }

        private DependencyType GetDependencyType(string packageName){
            var type = DependencyType.Standard;
            var root = _npmController.RootPackage;
            if (null != root){
                var match = root.Modules[packageName];
                if (null != match){
                    if (match.IsDevDependency){
                        type = DependencyType.Development;
                    } else if (match.IsOptionalDependency){
                        type = DependencyType.Optional;
                    }
                }
            }
            return type;
        }

        private async Task<bool> UninstallPackageAsync(string packageName, bool global){
            try{
                _command = new NpmUninstallCommand(
                    _npmController.FullPathToRootPackageDirectory,
                    packageName,
                    GetDependencyType(packageName),
                    global,
                    _npmController.PathToNpm,
                    _npmController.UseFallbackIfNpmNotFound);

                var retVal = await _command.ExecuteAsync();
                FireLogEvents(_command);
                _npmController.Refresh();
                return retVal;
            } catch (Exception e){
                OnExceptionLogged(e);
                return false;
            }
        }

        public async Task<bool> UninstallPackageAsync(string packageName){
            return await UninstallPackageAsync(packageName, false);
        }

        public async Task<bool> UninstallGlobalPackageAsync(string packageName){
            return await UninstallPackageAsync(packageName, true);
        }

        public async Task<IEnumerable<IPackage>> SearchAsync(string searchText){
            try{
                _command = new NpmSearchCommand(
                    _npmController.FullPathToRootPackageDirectory,
                    searchText,
                    _npmController.PathToNpm,
                    _npmController.UseFallbackIfNpmNotFound);
                var success = await _command.ExecuteAsync();
                FireLogEvents(_command);
                return success ? (_command as NpmSearchCommand).Results : new List<IPackage>();
            } catch (Exception e){
                OnExceptionLogged(e);
                return new List<IPackage>();
            }
        }

        public async Task<bool> UpdatePackagesAsync(){
            return await UpdatePackagesAsync(new List<IPackage>());
        }

        public async Task<bool> UpdatePackagesAsync(IEnumerable<IPackage> packages){
            try{
                _command = new NpmUpdateCommand(
                    _npmController.FullPathToRootPackageDirectory,
                    packages,
                    _npmController.PathToNpm,
                    _npmController.UseFallbackIfNpmNotFound);
                var success = await _command.ExecuteAsync();
                FireLogEvents(_command);
                _npmController.Refresh();
                return success;
            } catch (Exception e){
                OnExceptionLogged(e);
                return false;
            }
        }
    }
}
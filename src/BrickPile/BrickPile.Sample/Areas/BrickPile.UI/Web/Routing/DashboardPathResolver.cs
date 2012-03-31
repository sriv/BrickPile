﻿/* Copyright (C) 2011 by Marcus Lindblom

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

using System.Linq;
using System.Web;
using BrickPile.Core.Infrastructure.Indexes;
using BrickPile.Domain.Models;
using BrickPile.UI.Controllers;
using BrickPile.UI.Web.Mvc;
using Raven.Client;
using StructureMap;

namespace BrickPile.UI.Web.Routing {
    public class DashboardPathResolver : IPathResolver {
        private IDocumentSession _session;
        private readonly IPathData _pathData;
        private readonly IControllerMapper _controllerMapper;
        private IPageModel _pageModel;

        public IPathData ResolvePath(string virtualUrl) {
            // Set the default action to index
            _pathData.Action = ContentRoute.DefaultAction;
            // Set the default controller to content
            _pathData.Controller = ContentRoute.DefaultControllerName;
            // Get an up to date document session from structuremap
            _session = ObjectFactory.GetInstance<IDocumentSession>();
            // The requested url is for the start page with no action
            if (string.IsNullOrEmpty(virtualUrl) || string.Equals(virtualUrl,"/")) {
                _pageModel = _session.Query<IPageModel>()
                    .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                    .SingleOrDefault(x => x.Parent == null);
                _pathData.CurrentPageModel = _pageModel;
                
                return _pathData;
            }
            // Remove the trailing slash
            virtualUrl = VirtualPathUtility.RemoveTrailingSlash(virtualUrl);
            // The normal beahaviour should be to load the page based on the url
            _pageModel = _session.Query<IPageModel, Document_ByUrl>()
                    .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                    .FirstOrDefault(x => x.Metadata.Url == virtualUrl);
            // Try to load the page without the last segment of the url and set the last segment as action))
            if (_pageModel == null && virtualUrl.LastIndexOf("/", System.StringComparison.Ordinal) > 0) {
                var index = virtualUrl.LastIndexOf("/", System.StringComparison.Ordinal);
                var action = virtualUrl.Substring(index, virtualUrl.Length - index).Trim(new[] {'/'});
                virtualUrl = virtualUrl.Substring(0, index).TrimStart(new[] { '/' });
                _pageModel = _session.Query<IPageModel, Document_ByUrl>()
                    .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                    .FirstOrDefault(x => x.Metadata.Url == virtualUrl);
                _pathData.Action = action;
            }

            // If the page model still is empty, let's try to resolve if the start page has an action named (virtualUrl)
            if(_pageModel == null) {
                _pageModel = _session.Query<IPageModel>().SingleOrDefault(x => x.Parent == null);
                var controllerName = _controllerMapper.GetControllerName(typeof(ContentController));
                var action = virtualUrl.TrimStart(new[] {'/'});
                if(!_controllerMapper.ControllerHasAction(controllerName,action)) {
                    return null;
                }
                _pathData.Action = action;
            }

            if (_pageModel == null) {
                return null;
            }

            _pathData.CurrentPageModel = _pageModel;
            return _pathData;
        }

        public DashboardPathResolver(IDocumentSession session, IPathData pathData, IControllerMapper controllerMapper) {
            _session = session;
            _pathData = pathData;
            _controllerMapper = controllerMapper;
        }
    }
}
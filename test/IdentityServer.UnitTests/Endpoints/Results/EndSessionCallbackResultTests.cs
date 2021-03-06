﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using FluentAssertions;
using IdentityServer4.Configuration;
using IdentityServer4.Endpoints.Results;
using IdentityServer4.Extensions;
using IdentityServer4.UnitTests.Common;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IdentityServer4.UnitTests.Endpoints.Results
{
    public class EndSessionCallbackResultTests
    {
        private EndSessionCallbackResult _subject;

        private EndSessionCallbackValidationResult _result = new EndSessionCallbackValidationResult();
        private MockUserSession _mockUserSession = new MockUserSession();
        private IdentityServerOptions _options = TestIdentityServerOptions.Create();
        private StubBackChannelLogoutClient _stubBackChannelLogoutClient = new StubBackChannelLogoutClient();

        private DefaultHttpContext _context = new DefaultHttpContext();

        public EndSessionCallbackResultTests()
        {
            _context.SetIdentityServerOrigin("https://server");
            _context.SetIdentityServerBasePath("/");
            _context.Response.Body = new MemoryStream();

            _subject = new EndSessionCallbackResult(_result, _options, _stubBackChannelLogoutClient);
        }

        [Fact]
        public async Task error_should_return_400()
        {
            _result.IsError = true;

            await _subject.ExecuteAsync(_context);

            _context.Response.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task success_should_render_html_and_iframes()
        {
            _result.IsError = false;
            _result.FrontChannelLogoutUrls = new string[] { "http://foo.com", "http://bar.com" };

            await _subject.ExecuteAsync(_context);

            _context.Response.ContentType.Should().StartWith("text/html");
            _context.Response.Headers["Cache-Control"].First().Should().Contain("no-store");
            _context.Response.Headers["Cache-Control"].First().Should().Contain("no-cache");
            _context.Response.Headers["Cache-Control"].First().Should().Contain("max-age=0");
            _context.Response.Body.Seek(0, SeekOrigin.Begin);
            using (var rdr = new StreamReader(_context.Response.Body))
            {
                var html = rdr.ReadToEnd();
                html.Should().Contain("<iframe src='http://foo.com'></iframe>");
                html.Should().Contain("<iframe src='http://bar.com'></iframe>");
            }
        }

        [Fact]
        public async Task success_should_invoke_back_channel_clients()
        {
            _result.IsError = false;
            _result.BackChannelLogouts = new BackChannelLogoutModel[]
            {
                new BackChannelLogoutModel
                {
                    ClientId = "test",
                    LogoutUri = "http://test",
                    SessionId = "999",
                    SessionIdRequired = true,
                    SubjectId = "123"
                }
            };

            await _subject.ExecuteAsync(_context);

            _stubBackChannelLogoutClient.SendLogoutsWasCalled.Should().BeTrue();
        }
    }
}

﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OpenIdConnect.Server
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Client;
using AspNet.Security.OpenIdConnect.Primitives;
using Microsoft.Owin.Infrastructure;
using Microsoft.Owin.Security;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Owin.Security.OpenIdConnect.Extensions;
using Xunit;
using static System.Net.Http.HttpMethod;

namespace Owin.Security.OpenIdConnect.Server.Tests {
    public partial class OpenIdConnectServerHandlerTests {
        [Theory]
        [InlineData(nameof(Delete))]
        [InlineData(nameof(Head))]
        [InlineData(nameof(Options))]
        [InlineData(nameof(Put))]
        [InlineData(nameof(Trace))]
        public async Task InvokeIntrospectionEndpointAsync_UnexpectedMethodReturnsAnError(string method) {
            // Arrange
            var server = CreateAuthorizationServer();

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.SendAsync(method, IntrospectionEndpoint, new OpenIdConnectRequest());

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidRequest, response.Error);
            Assert.Equal("A malformed introspection request has been received: " +
                         "make sure to use either GET or POST.", response.ErrorDescription);
        }

        [Theory]
        [InlineData("custom_error", null, null)]
        [InlineData("custom_error", "custom_description", null)]
        [InlineData("custom_error", "custom_description", "custom_uri")]
        [InlineData(null, "custom_description", null)]
        [InlineData(null, "custom_description", "custom_uri")]
        [InlineData(null, null, "custom_uri")]
        [InlineData(null, null, null)]
        public async Task InvokeIntrospectionEndpointAsync_ExtractIntrospectionRequest_AllowsRejectingRequest(string error, string description, string uri) {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnExtractIntrospectionRequest = context => {
                    context.Reject(error, description, uri);

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest());

            // Assert
            Assert.Equal(error ?? OpenIdConnectConstants.Errors.InvalidRequest, response.Error);
            Assert.Equal(description, response.ErrorDescription);
            Assert.Equal(uri, response.ErrorUri);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_ExtractIntrospectionRequest_AllowsHandlingResponse() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnExtractIntrospectionRequest = context => {
                    context.HandleResponse();

                    context.OwinContext.Response.Headers["Content-Type"] = "application/json";

                    return context.OwinContext.Response.WriteAsync(JsonConvert.SerializeObject(new {
                        name = "Bob le Bricoleur"
                    }));
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.GetAsync(IntrospectionEndpoint);

            // Assert
            Assert.Equal("Bob le Bricoleur", (string) response["name"]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_ExtractIntrospectionRequest_AllowsSkippingToNextMiddleware() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnExtractIntrospectionRequest = context => {
                    context.SkipToNextMiddleware();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.GetAsync(IntrospectionEndpoint);

            // Assert
            Assert.Equal("Bob le Magnifique", (string) response["name"]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_MissingTokenCausesAnError() {
            // Arrange
            var server = CreateAuthorizationServer();

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = null
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidRequest, response.Error);
            Assert.Equal("A malformed introspection request has been received: a 'token' parameter " +
                         "with an access, refresh, or identity token is required.", response.ErrorDescription);
        }

        [Theory]
        [InlineData("custom_error", null, null)]
        [InlineData("custom_error", "custom_description", null)]
        [InlineData("custom_error", "custom_description", "custom_uri")]
        [InlineData(null, "custom_description", null)]
        [InlineData(null, "custom_description", "custom_uri")]
        [InlineData(null, null, "custom_uri")]
        [InlineData(null, null, null)]
        public async Task InvokeIntrospectionEndpointAsync_ValidateIntrospectionRequest_AllowsRejectingRequest(string error, string description, string uri) {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Reject(error, description, uri);

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal(error ?? OpenIdConnectConstants.Errors.InvalidRequest, response.Error);
            Assert.Equal(description, response.ErrorDescription);
            Assert.Equal(uri, response.ErrorUri);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_ValidateIntrospectionRequest_AllowsHandlingResponse() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.HandleResponse();

                    context.OwinContext.Response.Headers["Content-Type"] = "application/json";

                    return context.OwinContext.Response.WriteAsync(JsonConvert.SerializeObject(new {
                        name = "Bob le Magnifique"
                    }));
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal("Bob le Magnifique", (string) response["name"]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_ValidateIntrospectionRequest_AllowsSkippingToNextMiddleware() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.SkipToNextMiddleware();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal("Bob le Magnifique", (string) response["name"]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_MissingClientIdCausesAnErrorForValidatedRequests() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Validate();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = null,
                Token = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.ServerError, response.Error);
            Assert.Equal("An internal server error occurred.", response.ErrorDescription);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_InvalidTokenCausesAnError() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG"
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_ConfidentialTokenCausesAnErrorWhenValidationIsSkipped() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeRefreshToken = context => {
                    Assert.Equal("SlAV32hkKG", context.RefreshToken);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    // Mark the refresh token as private.
                    context.Ticket.SetProperty(OpenIdConnectConstants.Properties.ConfidentialityLevel,
                                               OpenIdConnectConstants.ConfidentialityLevels.Private);

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.RefreshToken
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_ExpiredTokenCausesAnError() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeRefreshToken = context => {
                    Assert.Equal("SlAV32hkKG", context.RefreshToken);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    context.Ticket.Properties.ExpiresUtc = context.Options.SystemClock.UtcNow - TimeSpan.FromDays(1);

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.RefreshToken
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_AuthorizationCodeCausesAnErrorWhenCallerIsNotAValidPresenter() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAuthorizationCode = context => {
                    Assert.Equal("SlAV32hkKG", context.AuthorizationCode);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    context.Ticket.SetPresenters("Contoso");

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = "Fabrikam",
                Token = "SlAV32hkKG",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AuthorizationCode
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_AccessTokenCausesAnErrorWhenCallerIsNotAValidAudienceOrPresenter() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAccessToken = context => {
                    Assert.Equal("2YotnFZFEjr1zCsicMWpAA", context.AccessToken);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    context.Ticket.SetAudiences("AdventureWorks");
                    context.Ticket.SetPresenters("Contoso");

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = "Fabrikam",
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_IdentityTokenCausesAnErrorWhenCallerIsNotAValidAudience() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeIdentityToken = context => {
                    Assert.Equal("2YotnFZFEjr1zCsicMWpAA", context.IdentityToken);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    context.Ticket.SetAudiences("AdventureWorks");

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = "Fabrikam",
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.IdToken
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_RefreshTokenCausesAnErrorWhenCallerIsNotAValidPresenter() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeRefreshToken = context => {
                    Assert.Equal("8xLOxBtZp8", context.RefreshToken);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    context.Ticket.SetPresenters("Contoso");

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = "Fabrikam",
                Token = "8xLOxBtZp8",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.RefreshToken
            });

            // Assert
            Assert.False((bool) response[OpenIdConnectConstants.Parameters.Active]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_BasicClaimsAreCorrectlyReturned() {
            // Arrange
            var clock = new Mock<ISystemClock>();
            clock.SetupGet(mock => mock.UtcNow)
                 .Returns(new DateTimeOffset(2016, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var server = CreateAuthorizationServer(options => {
                options.SystemClock = clock.Object;

                options.Provider.OnDeserializeAccessToken = context => {
                    Assert.Equal("2YotnFZFEjr1zCsicMWpAA", context.AccessToken);

                    var identity = new ClaimsIdentity(context.Options.AuthenticationType);
                    identity.AddClaim(ClaimTypes.NameIdentifier, "Bob le Magnifique");

                    context.Ticket = new AuthenticationTicket(identity, new AuthenticationProperties());
                    context.Ticket.SetAudiences("Fabrikam");
                    context.Ticket.SetTicketId("66B65AED-4033-4E9C-B975-A8CA7FB6FA79");

                    context.Ticket.Properties.IssuedUtc = new DateTimeOffset(2016, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    context.Ticket.Properties.ExpiresUtc = new DateTimeOffset(2017, 1, 1, 0, 0, 0, TimeSpan.Zero);

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Equal(9, response.GetParameters().Count());
            Assert.True((bool) response[OpenIdConnectConstants.Claims.Active]);
            Assert.Equal("66B65AED-4033-4E9C-B975-A8CA7FB6FA79", (string) response[OpenIdConnectConstants.Claims.JwtId]);
            Assert.Equal(OpenIdConnectConstants.TokenTypes.Bearer, (string) response[OpenIdConnectConstants.Claims.TokenType]);
            Assert.Equal(server.BaseAddress.AbsoluteUri, (string) response[OpenIdConnectConstants.Claims.Issuer]);
            Assert.Equal("Bob le Magnifique", (string) response[OpenIdConnectConstants.Claims.Subject]);
            Assert.Equal(1451606400, (long) response[OpenIdConnectConstants.Claims.IssuedAt]);
            Assert.Equal(1451606400, (long) response[OpenIdConnectConstants.Claims.NotBefore]);
            Assert.Equal(1483228800, (long) response[OpenIdConnectConstants.Claims.ExpiresAt]);
            Assert.Contains("Fabrikam", (JArray) response[OpenIdConnectConstants.Claims.Audience]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_NonBasicClaimsAreNotReturnedToUntrustedCallers() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAccessToken = context => {
                    Assert.Equal("2YotnFZFEjr1zCsicMWpAA", context.AccessToken);

                    var identity = new ClaimsIdentity(context.Options.AuthenticationType);
                    identity.AddClaim("custom_claim", "secret_value");

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    context.Ticket.SetAudiences("Contoso");

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = "Fabrikam",
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Null(response["custom_claim"]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_NonBasicClaimsAreReturnedToTrustedCallers() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAccessToken = context => {
                    Assert.Equal("2YotnFZFEjr1zCsicMWpAA", context.AccessToken);

                    var identity = new ClaimsIdentity(context.Options.AuthenticationType);
                    identity.AddClaim("custom_claim", "secret_value");

                    context.Ticket = new AuthenticationTicket(identity, new AuthenticationProperties());
                    context.Ticket.SetAudiences("Fabrikam");

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                ClientId = "Fabrikam",
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Equal("secret_value", (string) response["custom_claim"]);
        }

        [Theory]
        [InlineData("custom_error", null, null)]
        [InlineData("custom_error", "custom_description", null)]
        [InlineData("custom_error", "custom_description", "custom_uri")]
        [InlineData(null, "custom_description", null)]
        [InlineData(null, "custom_description", "custom_uri")]
        [InlineData(null, null, "custom_uri")]
        [InlineData(null, null, null)]
        public async Task InvokeIntrospectionEndpointAsync_HandleIntrospectionRequest_AllowsRejectingRequest(string error, string description, string uri) {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAuthorizationCode = context => {
                    Assert.Equal("SlAV32hkKG", context.AuthorizationCode);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };

                options.Provider.OnHandleIntrospectionRequest = context => {
                    context.Reject(error, description, uri);

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG"
            });

            // Assert
            Assert.Equal(error ?? OpenIdConnectConstants.Errors.InvalidRequest, response.Error);
            Assert.Equal(description, response.ErrorDescription);
            Assert.Equal(uri, response.ErrorUri);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_HandleIntrospectionRequest_AllowsHandlingResponse() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAuthorizationCode = context => {
                    Assert.Equal("SlAV32hkKG", context.AuthorizationCode);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };

                options.Provider.OnHandleIntrospectionRequest = context => {
                    context.HandleResponse();

                    context.OwinContext.Response.Headers["Content-Type"] = "application/json";

                    return context.OwinContext.Response.WriteAsync(JsonConvert.SerializeObject(new {
                        name = "Bob le Magnifique"
                    }));
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG"
            });

            // Assert
            Assert.Equal("Bob le Magnifique", (string) response["name"]);
        }

        [Fact]
        public async Task InvokeIntrospectionEndpointAsync_HandleIntrospectionRequest_AllowsSkippingToNextMiddleware() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAuthorizationCode = context => {
                    Assert.Equal("SlAV32hkKG", context.AuthorizationCode);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };

                options.Provider.OnHandleIntrospectionRequest = context => {
                    context.SkipToNextMiddleware();

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG"
            });

            // Assert
            Assert.Equal("Bob le Magnifique", (string) response["name"]);
        }

        [Fact]
        public async Task SendIntrospectionResponseAsync_ApplyIntrospectionResponse_AllowsHandlingResponse() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAuthorizationCode = context => {
                    Assert.Equal("SlAV32hkKG", context.AuthorizationCode);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };

                options.Provider.OnApplyIntrospectionResponse = context => {
                    context.HandleResponse();

                    context.OwinContext.Response.Headers["Content-Type"] = "application/json";

                    return context.OwinContext.Response.WriteAsync(JsonConvert.SerializeObject(new {
                        name = "Bob le Magnifique"
                    }));
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG"
            });

            // Assert
            Assert.Equal("Bob le Magnifique", (string) response["name"]);
        }

        [Fact]
        public async Task SendIntrospectionResponseAsync_ApplyIntrospectionResponse_ResponseContainsCustomParameters() {
            // Arrange
            var server = CreateAuthorizationServer(options => {
                options.Provider.OnDeserializeAuthorizationCode = context => {
                    Assert.Equal("SlAV32hkKG", context.AuthorizationCode);

                    context.Ticket = new AuthenticationTicket(
                        new ClaimsIdentity(context.Options.AuthenticationType),
                        new AuthenticationProperties());

                    return Task.FromResult(0);
                };

                options.Provider.OnValidateIntrospectionRequest = context => {
                    context.Skip();

                    return Task.FromResult(0);
                };

                options.Provider.OnApplyIntrospectionResponse = context => {
                    context.Response["custom_parameter"] = "custom_value";

                    return Task.FromResult(0);
                };
            });

            var client = new OpenIdConnectClient(server.HttpClient);

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest {
                Token = "SlAV32hkKG"
            });

            // Assert
            Assert.Equal("custom_value", (string) response["custom_parameter"]);
        }
    }
}

﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OpenIdConnect.Server
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using AspNet.Security.OpenIdConnect.Primitives;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Authentication;

namespace AspNet.Security.OpenIdConnect.Extensions {
    /// <summary>
    /// Provides extension methods to make <see cref="AuthenticationTicket"/> easier to use.
    /// </summary>
    public static class OpenIdConnectExtensions {
        /// <summary>
        /// Gets the destinations associated with a claim.
        /// </summary>
        /// <param name="claim">The <see cref="Claim"/> instance.</param>
        /// <returns>The destinations associated with the claim.</returns>
        public static IEnumerable<string> GetDestinations([NotNull] this Claim claim) {
            if (claim == null) {
                throw new ArgumentNullException(nameof(claim));
            }

            string destinations;
            claim.Properties.TryGetValue(OpenIdConnectConstants.Properties.Destinations, out destinations);

            return destinations?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries)
                               ?.Distinct(StringComparer.OrdinalIgnoreCase)
                   ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Determines whether the given claim
        /// contains the required destination.
        /// </summary>
        /// <param name="claim">The <see cref="Claim"/> instance.</param>
        /// <param name="destination">The required destination.</param>
        public static bool HasDestination([NotNull] this Claim claim, string destination) {
            if (claim == null) {
                throw new ArgumentNullException(nameof(claim));
            }

            string destinations;
            if (!claim.Properties.TryGetValue(OpenIdConnectConstants.Properties.Destinations, out destinations)) {
                return false;
            }

            return HasValue(destinations, destination, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds specific destinations to a claim.
        /// </summary>
        /// <param name="claim">The <see cref="Claim"/> instance.</param>
        /// <param name="destinations">The destinations.</param>
        public static Claim SetDestinations([NotNull] this Claim claim, IEnumerable<string> destinations) {
            if (claim == null) {
                throw new ArgumentNullException(nameof(claim));
            }

            if (destinations == null || !destinations.Any()) {
                claim.Properties.Remove(OpenIdConnectConstants.Properties.Destinations);

                return claim;
            }

            if (destinations.Any(destination => destination.Contains(" "))) {
                throw new ArgumentException("Destinations cannot contain spaces.", nameof(destinations));
            }

            claim.Properties[OpenIdConnectConstants.Properties.Destinations] =
                string.Join(" ", destinations.Distinct(StringComparer.OrdinalIgnoreCase));

            return claim;
        }

        /// <summary>
        /// Adds specific destinations to a claim.
        /// </summary>
        /// <param name="claim">The <see cref="Claim"/> instance.</param>
        /// <param name="destinations">The destinations.</param>
        public static Claim SetDestinations([NotNull] this Claim claim, params string[] destinations) {
            // Note: guarding the destinations parameter against null values
            // is not necessary as AsEnumerable() doesn't throw on null values.
            return claim.SetDestinations(destinations.AsEnumerable());
        }

        /// <summary>
        /// Clones an identity by filtering its claims and the claims of its actor, recursively.
        /// </summary>
        /// <param name="identity">The <see cref="ClaimsIdentity"/> instance to filter.</param>
        /// <param name="filter">
        /// The delegate filtering the claims: return <c>true</c>
        /// to accept the claim, <c>false</c> to remove it.
        /// </param>
        public static ClaimsIdentity Clone(
            [NotNull] this ClaimsIdentity identity,
            [NotNull] Func<Claim, bool> filter) {
            if (identity == null) {
                throw new ArgumentNullException(nameof(identity));
            }

            if (filter == null) {
                throw new ArgumentNullException(nameof(filter));
            }

            var clone = identity.Clone();

            // Note: make sure to call ToArray() to avoid modifying
            // the initial collection iterated by ClaimsIdentity.Claims.
            foreach (var claim in clone.Claims.ToArray()) {
                if (!filter(claim)) {
                    clone.RemoveClaim(claim);
                }
            }

            if (clone.Actor != null) {
                clone.Actor = clone.Actor.Clone(filter);
            }

            return clone;
        }

        /// <summary>
        /// Clones a principal by filtering its identities.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> instance to filter.</param>
        /// <param name="filter">
        /// The delegate filtering the claims: return <c>true</c>
        /// to accept the claim, <c>false</c> to remove it.
        /// </param>
        public static ClaimsPrincipal Clone(
            [NotNull] this ClaimsPrincipal principal,
            [NotNull] Func<Claim, bool> filter) {
            if (principal == null) {
                throw new ArgumentNullException(nameof(principal));
            }

            if (filter == null) {
                throw new ArgumentNullException(nameof(filter));
            }

            var clone = new ClaimsPrincipal();

            foreach (var identity in principal.Identities) {
                clone.AddIdentity(identity.Clone(filter));
            }

            return clone;
        }

        /// <summary>
        /// Adds a claim to a given identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="type">The type associated with the claim.</param>
        /// <param name="value">The value associated with the claim.</param>
        public static ClaimsIdentity AddClaim(
            [NotNull] this ClaimsIdentity identity,
            [NotNull] string type, [NotNull] string value) {
            if (identity == null) {
                throw new ArgumentNullException(nameof(identity));
            }

            if (string.IsNullOrEmpty(type)) {
                throw new ArgumentException("The claim type cannot be null or empty.", nameof(type));
            }

            if (string.IsNullOrEmpty(value)) {
                throw new ArgumentException("The claim value cannot be null or empty.", nameof(value));
            }

            identity.AddClaim(new Claim(type, value));
            return identity;
        }

        /// <summary>
        /// Adds a claim to a given identity and specify one or more destinations.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="type">The type associated with the claim.</param>
        /// <param name="value">The value associated with the claim.</param>
        /// <param name="destinations">The destinations associated with the claim.</param>
        public static ClaimsIdentity AddClaim(
            [NotNull] this ClaimsIdentity identity,
            [NotNull] string type, [NotNull] string value,
            [NotNull] IEnumerable<string> destinations) {
            if (identity == null) {
                throw new ArgumentNullException(nameof(identity));
            }

            if (string.IsNullOrEmpty(type)) {
                throw new ArgumentException("The claim type cannot be null or empty.", nameof(type));
            }

            if (string.IsNullOrEmpty(value)) {
                throw new ArgumentException("The claim value cannot be null or empty.", nameof(value));
            }

            if (destinations == null) {
                throw new ArgumentNullException(nameof(destinations));
            }

            identity.AddClaim(new Claim(type, value).SetDestinations(destinations));
            return identity;
        }

        /// <summary>
        /// Adds a claim to a given identity and specify one or more destinations.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="type">The type associated with the claim.</param>
        /// <param name="value">The value associated with the claim.</param>
        /// <param name="destinations">The destinations associated with the claim.</param>
        public static ClaimsIdentity AddClaim(
            [NotNull] this ClaimsIdentity identity,
            [NotNull] string type, [NotNull] string value,
            [NotNull] params string[] destinations) {
            // Note: guarding the destinations parameter against null values
            // is not necessary as AsEnumerable() doesn't throw on null values.
            return identity.AddClaim(type, value, destinations.AsEnumerable());
        }

        /// <summary>
        /// Gets the claim value corresponding to the given type.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="type">The type associated with the claim.</param>
        /// <returns>The claim value.</returns>
        public static string GetClaim([NotNull] this ClaimsIdentity identity, [NotNull] string type) {
            if (identity == null) {
                throw new ArgumentNullException(nameof(identity));
            }

            if (string.IsNullOrEmpty(type)) {
                throw new ArgumentException("The claim type cannot be null or empty.", nameof(type));
            }

            return identity.FindFirst(type)?.Value;
        }

        /// <summary>
        /// Gets the claim value corresponding to the given type.
        /// </summary>
        /// <param name="principal">The principal.</param>
        /// <param name="type">The type associated with the claim.</param>
        /// <returns>The claim value.</returns>
        public static string GetClaim([NotNull] this ClaimsPrincipal principal, [NotNull] string type) {
            if (principal == null) {
                throw new ArgumentNullException(nameof(principal));
            }

            if (string.IsNullOrEmpty(type)) {
                throw new ArgumentException("The claim type cannot be null or empty.", nameof(type));
            }

            return principal.FindFirst(type)?.Value;
        }

        /// <summary>
        /// Copies the authentication properties in a new instance.
        /// </summary>
        /// <param name="properties">The authentication properties to copy.</param>
        /// <returns>A new instance containing the copied properties.</returns>
        public static AuthenticationProperties Copy([NotNull] this AuthenticationProperties properties) {
            if (properties == null) {
                throw new ArgumentNullException(nameof(properties));
            }

            return new AuthenticationProperties(properties.Items.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        /// <summary>
        /// Copies the authentication ticket in a new instance.
        /// </summary>
        /// <param name="ticket">The authentication ticket to copy.</param>
        /// <returns>A new instance containing the copied ticket</returns>
        public static AuthenticationTicket Copy([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            // Note: don't use ClaimsIdentity.Clone() as it doesn't work properly on <.NET 4.6.
            var principal = new ClaimsPrincipal(ticket.Principal.Clone(claim => true));

            return new AuthenticationTicket(principal, ticket.Properties.Copy(), ticket.AuthenticationScheme);
        }

        /// <summary>
        /// Gets a given property from the authentication properties.
        /// </summary>
        /// <param name="properties">The authentication properties.</param>
        /// <param name="property">The specific property to look for.</param>
        /// <returns>The value corresponding to the property, or <c>null</c> if the property cannot be found.</returns>
        public static string GetProperty([NotNull] this AuthenticationProperties properties, [NotNull] string property) {
            if (properties == null) {
                throw new ArgumentNullException(nameof(properties));
            }

            if (string.IsNullOrEmpty(property)) {
                throw new ArgumentException("The property name cannot be null or empty.", nameof(property));
            }

            string value;
            if (!properties.Items.TryGetValue(property, out value)) {
                return null;
            }

            return value;
        }

        /// <summary>
        /// Gets a given property from the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="property">The specific property to look for.</param>
        /// <returns>The value corresponding to the property, or <c>null</c> if the property cannot be found.</returns>
        public static string GetProperty([NotNull] this AuthenticationTicket ticket, [NotNull] string property) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.Properties.GetProperty(property);
        }

        /// <summary>
        /// Gets the audiences list stored in the authentication ticket.
        /// Note: this method automatically excludes duplicate audiences.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The audiences list or <c>Enumerable.Empty</c> is the property cannot be found.</returns>
        public static IEnumerable<string> GetAudiences([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.GetProperty(OpenIdConnectConstants.Properties.Audiences)
                        ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries)
                        ?.Distinct(StringComparer.Ordinal)
                   ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the presenters list stored in the authentication ticket.
        /// Note: this method automatically excludes duplicate presenters.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The presenters list or <c>Enumerable.Empty</c> is the property cannot be found.</returns>
        public static IEnumerable<string> GetPresenters([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.GetProperty(OpenIdConnectConstants.Properties.Presenters)
                        ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries)
                        ?.Distinct(StringComparer.Ordinal)
                   ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the resources list stored in the authentication ticket.
        /// Note: this method automatically excludes duplicate resources.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The resources list or <c>Enumerable.Empty</c> is the property cannot be found.</returns>
        public static IEnumerable<string> GetResources([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.GetProperty(OpenIdConnectConstants.Properties.Resources)
                        ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries)
                        ?.Distinct(StringComparer.Ordinal)
                   ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the scopes list stored in the authentication ticket.
        /// Note: this method automatically excludes duplicate scopes.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The scopes list or <c>Enumerable.Empty</c> is the property cannot be found.</returns>
        public static IEnumerable<string> GetScopes([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.GetProperty(OpenIdConnectConstants.Properties.Scopes)
                        ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries)
                        ?.Distinct(StringComparer.Ordinal)
                   ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the access token lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The access token lifetime or <c>null</c> is the property cannot be found.</returns>

        public static TimeSpan? GetAccessTokenLifetime([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.AccessTokenLifetime);
            if (string.IsNullOrEmpty(value)) {
                return null;
            }

            TimeSpan result;
            if (TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out result)) {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the authorization code lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The authorization code lifetime or <c>null</c> is the property cannot be found.</returns>

        public static TimeSpan? GetAuthorizationCodeLifetime([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.AuthorizationCodeLifetime);
            if (string.IsNullOrEmpty(value)) {
                return null;
            }

            TimeSpan result;
            if (TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out result)) {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the identity token lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The identity token lifetime or <c>null</c> is the property cannot be found.</returns>

        public static TimeSpan? GetIdentityTokenLifetime([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.IdentityTokenLifetime);
            if (string.IsNullOrEmpty(value)) {
                return null;
            }

            TimeSpan result;
            if (TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out result)) {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the refresh token lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The refresh token lifetime or <c>null</c> is the property cannot be found.</returns>

        public static TimeSpan? GetRefreshTokenLifetime([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.RefreshTokenLifetime);
            if (string.IsNullOrEmpty(value)) {
                return null;
            }

            TimeSpan result;
            if (TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out result)) {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the unique identifier associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The unique identifier or <c>null</c> is the property cannot be found.</returns>
        public static string GetTicketId([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.GetProperty(OpenIdConnectConstants.Properties.TicketId);
        }

        /// <summary>
        /// Gets the usage of the token stored in the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The usage of the token or <c>null</c> is the property cannot be found.</returns>
        public static string GetUsage([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.GetProperty(OpenIdConnectConstants.Properties.Usage);
        }

        /// <summary>
        /// Determines whether a given exists in the authentication properties.
        /// </summary>
        /// <param name="properties">The authentication properties.</param>
        /// <param name="property">The specific property to look for.</param>
        /// <returns><c>true</c> if the property was found, <c>false</c> otherwise.</returns>
        public static bool HasProperty([NotNull] this AuthenticationProperties properties, [NotNull] string property) {
            if (properties == null) {
                throw new ArgumentNullException(nameof(properties));
            }

            if (string.IsNullOrEmpty(property)) {
                throw new ArgumentException($"{nameof(property)} cannot be null or empty.", nameof(property));
            }

            string value;
            if (!properties.Items.TryGetValue(property, out value)) {
                return false;
            }

            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Determines whether a given exists in the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="property">The specific property to look for.</param>
        /// <returns><c>true</c> if the property was found, <c>false</c> otherwise.</returns>
        public static bool HasProperty([NotNull] this AuthenticationTicket ticket, [NotNull] string property) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.Properties.HasProperty(property);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains at least one audience.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket contains at least one audience.</returns>
        public static bool HasAudience([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.HasProperty(OpenIdConnectConstants.Properties.Audiences);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains the given audience.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="audience">The audience.</param>
        /// <returns><c>true</c> if the ticket contains the given audience.</returns>
        public static bool HasAudience([NotNull] this AuthenticationTicket ticket, string audience) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var audiences = ticket.GetProperty(OpenIdConnectConstants.Properties.Audiences)
                                 ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries);

            if (audiences == null) {
                return false;
            }

            return audiences.Contains(audience, StringComparer.Ordinal);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains at least one presenter.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket contains at least one presenter.</returns>
        public static bool HasPresenter([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.HasProperty(OpenIdConnectConstants.Properties.Presenters);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains the given presenter.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="presenter">The presenter.</param>
        /// <returns><c>true</c> if the ticket contains the given presenter.</returns>
        public static bool HasPresenter([NotNull] this AuthenticationTicket ticket, string presenter) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var presenters = ticket.GetProperty(OpenIdConnectConstants.Properties.Presenters)
                                  ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries);

            if (presenters == null) {
                return false;
            }

            return presenters.Contains(presenter, StringComparer.Ordinal);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains at least one resource.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket contains at least one resource.</returns>
        public static bool HasResource([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.HasProperty(OpenIdConnectConstants.Properties.Resources);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains the given resource.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="resource">The resource.</param>
        /// <returns><c>true</c> if the ticket contains the given resource.</returns>
        public static bool HasResource([NotNull] this AuthenticationTicket ticket, string resource) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var resources = ticket.GetProperty(OpenIdConnectConstants.Properties.Resources)
                                 ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries);

            if (resources == null) {
                return false;
            }

            return resources.Contains(resource, StringComparer.Ordinal);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains at least one scope.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket contains at least one scope.</returns>
        public static bool HasScope([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.HasProperty(OpenIdConnectConstants.Properties.Scopes);
        }

        /// <summary>
        /// Determines whether the authentication ticket contains the given scope.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="scope">The scope.</param>
        /// <returns><c>true</c> if the ticket contains the given scope.</returns>
        public static bool HasScope([NotNull] this AuthenticationTicket ticket, string scope) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var scopes = ticket.GetProperty(OpenIdConnectConstants.Properties.Scopes)
                              ?.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries);

            if (scopes == null) {
                return false;
            }

            return scopes.Contains(scope, StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets a boolean value indicating whether
        /// the ticket is marked as confidential.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket is confidential, or <c>false</c> if it's not.</returns>
        public static bool IsConfidential([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.ConfidentialityLevel);
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            return string.Equals(value, OpenIdConnectConstants.ConfidentialityLevels.Private, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the
        /// authentication ticket corresponds to an access token.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket corresponds to an access token.</returns>
        public static bool IsAccessToken([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.Usage);
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            return string.Equals(value, OpenIdConnectConstants.Usages.AccessToken, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the
        /// authentication ticket corresponds to an access token.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket corresponds to an authorization code.</returns>
        public static bool IsAuthorizationCode([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.Usage);
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            return string.Equals(value, OpenIdConnectConstants.Usages.AuthorizationCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the
        /// authentication ticket corresponds to an identity token.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket corresponds to an identity token.</returns>
        public static bool IsIdentityToken([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.Usage);
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            return string.Equals(value, OpenIdConnectConstants.Usages.IdentityToken, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the
        /// authentication ticket corresponds to a refresh token.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns><c>true</c> if the ticket corresponds to a refresh token.</returns>
        public static bool IsRefreshToken([NotNull] this AuthenticationTicket ticket) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = ticket.GetProperty(OpenIdConnectConstants.Properties.Usage);
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            return string.Equals(value, OpenIdConnectConstants.Usages.RefreshToken, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds, updates or removes a given property in the authentication properties.
        /// </summary>
        /// <param name="properties">The authentication properties.</param>
        /// <param name="property">The specific property to add, update or remove.</param>
        /// <param name="value">The value associated with the property.</param>
        /// <returns>The authentication properties.</returns>
        public static AuthenticationProperties SetProperty(
            [NotNull] this AuthenticationProperties properties,
            [NotNull] string property, [CanBeNull] string value) {
            if (properties == null) {
                throw new ArgumentNullException(nameof(properties));
            }

            if (string.IsNullOrEmpty(property)) {
                throw new ArgumentException("The property name cannot be null or empty.", nameof(property));
            }

            if (string.IsNullOrEmpty(value)) {
                properties.Items.Remove(property);

                return properties;
            }

            properties.Items[property] = value;

            return properties;
        }

        /// <summary>
        /// Adds, updates or removes a given property in the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="property">The specific property to add, update or remove.</param>
        /// <param name="value">The value associated with the property.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetProperty(
            [NotNull] this AuthenticationTicket ticket,
            [NotNull] string property, [CanBeNull] string value) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            ticket.Properties.SetProperty(property, value);

            return ticket;
        }

        /// <summary>
        /// Sets the audiences list in the authentication ticket.
        /// Note: this method automatically excludes duplicate audiences.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="audiences">The audiences to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetAudiences([NotNull] this AuthenticationTicket ticket, IEnumerable<string> audiences) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (audiences == null || !audiences.Any()) {
                ticket.Properties.Items.Remove(OpenIdConnectConstants.Properties.Audiences);

                return ticket;
            }

            if (audiences.Any(audience => string.IsNullOrEmpty(audience) || audience.Contains(" "))) {
                throw new ArgumentException("The audiences cannot be null, empty or contain spaces.", nameof(audiences));
            }

            ticket.Properties.Items[OpenIdConnectConstants.Properties.Audiences] =
                string.Join(" ", audiences.Distinct(StringComparer.Ordinal));

            return ticket;
        }

        /// <summary>
        /// Sets the audiences list in the authentication ticket.
        /// Note: this method automatically excludes duplicate audiences.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="audiences">The audiences to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetAudiences([NotNull] this AuthenticationTicket ticket, params string[] audiences) {
            // Note: guarding the audiences parameter against null values
            // is not necessary as AsEnumerable() doesn't throw on null values.
            return ticket.SetAudiences(audiences.AsEnumerable());
        }

        /// <summary>
        /// Sets the presenters list in the authentication ticket.
        /// Note: this method automatically excludes duplicate presenters.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="presenters">The presenters to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetPresenters([NotNull] this AuthenticationTicket ticket, IEnumerable<string> presenters) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (presenters == null || !presenters.Any()) {
                ticket.Properties.Items.Remove(OpenIdConnectConstants.Properties.Presenters);

                return ticket;
            }

            if (presenters.Any(presenter => string.IsNullOrEmpty(presenter) || presenter.Contains(" "))) {
                throw new ArgumentException("The presenters cannot be null, empty or contain spaces.", nameof(presenters));
            }

            ticket.Properties.Items[OpenIdConnectConstants.Properties.Presenters] =
                string.Join(" ", presenters.Distinct(StringComparer.Ordinal));

            return ticket;
        }

        /// <summary>
        /// Sets the presenters list in the authentication ticket.
        /// Note: this method automatically excludes duplicate presenters.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="presenters">The presenters to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetPresenters([NotNull] this AuthenticationTicket ticket, params string[] presenters) {
            // Note: guarding the presenters parameter against null values
            // is not necessary as AsEnumerable() doesn't throw on null values.
            return ticket.SetPresenters(presenters.AsEnumerable());
        }

        /// <summary>
        /// Sets the resources list in the authentication ticket.
        /// Note: this method automatically excludes duplicate resources.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="resources">The resources to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetResources([NotNull] this AuthenticationTicket ticket, IEnumerable<string> resources) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (resources == null || !resources.Any()) {
                ticket.Properties.Items.Remove(OpenIdConnectConstants.Properties.Resources);

                return ticket;
            }

            if (resources.Any(resource => string.IsNullOrEmpty(resource) || resource.Contains(" "))) {
                throw new ArgumentException("The resources cannot be null, empty or contain spaces.", nameof(resources));
            }

            ticket.Properties.Items[OpenIdConnectConstants.Properties.Resources] =
                string.Join(" ", resources.Distinct(StringComparer.Ordinal));

            return ticket;
        }

        /// <summary>
        /// Sets the resources list in the authentication ticket.
        /// Note: this method automatically excludes duplicate resources.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="resources">The resources to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetResources([NotNull] this AuthenticationTicket ticket, params string[] resources) {
            // Note: guarding the resources parameter against null values
            // is not necessary as AsEnumerable() doesn't throw on null values.
            return ticket.SetResources(resources.AsEnumerable());
        }

        /// <summary>
        /// Sets the scopes list in the authentication ticket.
        /// Note: this method automatically excludes duplicate scopes.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="scopes">The scopes to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetScopes([NotNull] this AuthenticationTicket ticket, IEnumerable<string> scopes) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (scopes == null || !scopes.Any()) {
                ticket.Properties.Items.Remove(OpenIdConnectConstants.Properties.Scopes);

                return ticket;
            }

            if (scopes.Any(scope => string.IsNullOrEmpty(scope) || scope.Contains(" "))) {
                throw new ArgumentException("The scopes cannot be null, empty or contain spaces.", nameof(scopes));
            }

            ticket.Properties.Items[OpenIdConnectConstants.Properties.Scopes] =
                string.Join(" ", scopes.Distinct(StringComparer.Ordinal));

            return ticket;
        }

        /// <summary>
        /// Sets the scopes list in the authentication ticket.
        /// Note: this method automatically excludes duplicate scopes.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="scopes">The scopes to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetScopes([NotNull] this AuthenticationTicket ticket, params string[] scopes) {
            // Note: guarding the scopes parameter against null values
            // is not necessary as AsEnumerable() doesn't throw on null values.
            return ticket.SetScopes(scopes.AsEnumerable());
        }

        /// <summary>
        /// Sets the access token lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="lifetime">The access token lifetime to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetAccessTokenLifetime([NotNull] this AuthenticationTicket ticket, TimeSpan? lifetime) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = lifetime?.ToString("c", CultureInfo.InvariantCulture);

            return ticket.SetProperty(OpenIdConnectConstants.Properties.AccessTokenLifetime, value);
        }

        /// <summary>
        /// Sets the authorization code lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="lifetime">The authorization code lifetime to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetAuthorizationCodeLifetime([NotNull] this AuthenticationTicket ticket, TimeSpan? lifetime) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = lifetime?.ToString("c", CultureInfo.InvariantCulture);

            return ticket.SetProperty(OpenIdConnectConstants.Properties.AuthorizationCodeLifetime, value);
        }

        /// <summary>
        /// Sets the identity token lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="lifetime">The identity token lifetime to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetIdentityTokenLifetime([NotNull] this AuthenticationTicket ticket, TimeSpan? lifetime) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = lifetime?.ToString("c", CultureInfo.InvariantCulture);

            return ticket.SetProperty(OpenIdConnectConstants.Properties.IdentityTokenLifetime, value);
        }

        /// <summary>
        /// Sets the refresh token lifetime associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="lifetime">The refresh token lifetime to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetRefreshTokenLifetime([NotNull] this AuthenticationTicket ticket, TimeSpan? lifetime) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            var value = lifetime?.ToString("c", CultureInfo.InvariantCulture);

            return ticket.SetProperty(OpenIdConnectConstants.Properties.RefreshTokenLifetime, value);
        }

        /// <summary>
        /// Sets the unique identifier associated with the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="identifier">The unique identifier to store.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetTicketId([NotNull] this AuthenticationTicket ticket, string identifier) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.SetProperty(OpenIdConnectConstants.Properties.TicketId, identifier);
        }

        /// <summary>
        /// Sets the usage of the token in the authentication ticket.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <param name="usage">The usage of the token.</param>
        /// <returns>The authentication ticket.</returns>
        public static AuthenticationTicket SetUsage([NotNull] this AuthenticationTicket ticket, string usage) {
            if (ticket == null) {
                throw new ArgumentNullException(nameof(ticket));
            }

            return ticket.SetProperty(OpenIdConnectConstants.Properties.Usage, usage);
        }

        private static bool HasValue(string source, string value, StringComparison comparison = StringComparison.Ordinal) {
            if (string.IsNullOrEmpty(source)) {
                return false;
            }

            return (from component in source.Split(OpenIdConnectConstants.Separators.Space, StringSplitOptions.RemoveEmptyEntries)
                    where string.Equals(component, value, comparison)
                    select component).Any();
        }
    }
}

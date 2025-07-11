// <copyright file="OpenIdConfiguration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Infra.OAuth.Models;

public class OpenIdConfiguration
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; set; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; set; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; set; }

    [JsonPropertyName("userinfo_endpoint")]
    public required string UserinfoEndpoint { get; set; }

    [JsonPropertyName("device_authorization_endpoint")]
    public required string DeviceAuthorizationEndpoint { get; set; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; set; }

    [JsonPropertyName("introspection_endpoint")]
    public required string IntrospectionEndpoint { get; set; }

    [JsonPropertyName("response_types_supported")]
    public required List<string> ResponseTypesSupported { get; set; }

    [JsonPropertyName("response_modes_supported")]
    public required List<string> ResponseModesSupported { get; set; }

    [JsonPropertyName("grant_types_supported")]
    public required List<string> GrantTypesSupported { get; set; }

    [JsonPropertyName("subject_types_supported")]
    public required List<string> SubjectTypesSupported { get; set; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public required List<string> IdTokenSigningAlgValuesSupported { get; set; }

    [JsonPropertyName("scopes_supported")]
    public required List<string> ScopesSupported { get; set; }

    [JsonPropertyName("claims_supported")]
    public required List<string> ClaimsSupported { get; set; }

    [JsonPropertyName("request_parameter_supported")]
    public bool RequestParameterSupported { get; set; }

    [JsonPropertyName("request_object_signing_alg_values_supported")]
    public required List<string> RequestObjectSigningAlgValuesSupported { get; set; }

    [JsonPropertyName("end_session_endpoint")]
    public required string EndSessionEndpoint { get; set; }
}
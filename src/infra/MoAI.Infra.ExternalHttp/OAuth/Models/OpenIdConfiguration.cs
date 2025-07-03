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
    public string Issuer { get; set; }

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; }

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; }

    [JsonPropertyName("userinfo_endpoint")]
    public string UserinfoEndpoint { get; set; }

    [JsonPropertyName("device_authorization_endpoint")]
    public string DeviceAuthorizationEndpoint { get; set; }

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; }

    [JsonPropertyName("introspection_endpoint")]
    public string IntrospectionEndpoint { get; set; }

    [JsonPropertyName("response_types_supported")]
    public List<string> ResponseTypesSupported { get; set; }

    [JsonPropertyName("response_modes_supported")]
    public List<string> ResponseModesSupported { get; set; }

    [JsonPropertyName("grant_types_supported")]
    public List<string> GrantTypesSupported { get; set; }

    [JsonPropertyName("subject_types_supported")]
    public List<string> SubjectTypesSupported { get; set; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public List<string> IdTokenSigningAlgValuesSupported { get; set; }

    [JsonPropertyName("scopes_supported")]
    public List<string> ScopesSupported { get; set; }

    [JsonPropertyName("claims_supported")]
    public List<string> ClaimsSupported { get; set; }

    [JsonPropertyName("request_parameter_supported")]
    public bool RequestParameterSupported { get; set; }

    [JsonPropertyName("request_object_signing_alg_values_supported")]
    public List<string> RequestObjectSigningAlgValuesSupported { get; set; }

    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; set; }
}
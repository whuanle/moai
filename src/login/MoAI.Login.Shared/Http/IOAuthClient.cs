// <copyright file="IOAuthClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Login.Http;

public interface IOAuthClient
{
    public HttpClient Client { get; }

    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdConfiguration> GetWellKnownAsync(string path);
}

public interface IOAuthClientAccessToken
{
    public HttpClient Client { get; }

    [Post("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdAuthorizationResponse> GetAccessTokenAsync(string path, [Body] OpenIdAuthorizationRequest request);

    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdUserProfile> GetUserInfoAsync(string path, [Query] string accessToken);
}

public class OpenIdUserProfile
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; }

    [JsonPropertyName("iss")]
    public string Issuer { get; set; }

    [JsonPropertyName("aud")]
    public string Audience { get; set; }

    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("picture")]
    public string Picture { get; set; }
}

public class OpenIdAuthorizationResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}
public class OpenIdAuthorizationRequest
{
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }
}

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
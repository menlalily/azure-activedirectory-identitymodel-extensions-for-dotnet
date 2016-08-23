﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Logging;
using System.Text;

namespace Microsoft.IdentityModel.Tokens
{
    public class AesCbcHmacSha2Provider : IDecryptionProvider, IEncryptionProvider
    {
        // Used for encrypting.
        private byte[] _cek;
        private AesCbcHmacSha2 _algorithm;
        private byte[] _authenticatedData;

        // Used for decrypting.
        private AuthenticatedEncryptionParameters _authenticatedEncryptionParameters;

        public AesCbcHmacSha2Provider(SecurityKey key, string algorithm, AuthenticatedEncryptionParameters authenticatedEncryptionParameters, string encodedProtectHeader)
        {
            if (key != null && authenticatedEncryptionParameters != null)
                // TODO (Yan) : Add exception log message and throw;
                throw LogHelper.LogArgumentException<ArgumentException>(nameof(key), "Key and authenticatedEncryptionParameters cannot have value at the same time.");

            if (string.IsNullOrWhiteSpace(encodedProtectHeader))
                // TODO (Yan) : Add exception log message and throw;
                throw LogHelper.LogArgumentException<ArgumentException>(nameof(encodedProtectHeader), "Encoded Protect Header could not be null or empty.");

            _algorithm = ResolveAlgorithm(algorithm);

            if (authenticatedEncryptionParameters != null)
            {
                // For Decrypting
                ValidateKeySize(authenticatedEncryptionParameters.Key, algorithm);
                _authenticatedEncryptionParameters = authenticatedEncryptionParameters;

            }
            else
            {
                // For Encrypting              
                if (key != null)
                {
                    // try to use the provided key directly.
                    SymmetricSecurityKey symmetricSecurityKey = key as SymmetricSecurityKey;
                    if (symmetricSecurityKey != null)
                        _cek = symmetricSecurityKey.Key;
                    else
                    {
                        JsonWebKey jsonWebKey = key as JsonWebKey;
                        if (jsonWebKey != null && jsonWebKey.K != null)
                            _cek = Base64UrlEncoder.DecodeBytes(jsonWebKey.K);
                    }

                    if (_cek == null)
                        // TODO (Yan) : Add log messages for this
                        throw LogHelper.LogArgumentException<ArgumentException>(nameof(key), LogMessages.IDX10703);

                    ValidateKeySize(_cek, algorithm);
                }
            }

            _authenticatedData = Encoding.ASCII.GetBytes(encodedProtectHeader);
        }

        public byte[] Encrypt(byte[] plaintext, out object extraOutputs)
        {
            if (plaintext == null)
                throw LogHelper.LogArgumentNullException("plaintext");

            if (plaintext.Length == 0)
                throw LogHelper.LogException<ArgumentException>("Cannot encrypt empty 'plaintext'");

            // Generate random CEK.
            if (_cek == null)
                _cek = GenerateCEK();

            AuthenticatedEncryptionParameters authenticatedEncryptionParameters = new AuthenticatedEncryptionParameters()
            {
                Key = _cek,
                InitialVector = GenerateIV()
            };

            IAuthenticatedCryptoTransform authenticatedCryptoTransform = (IAuthenticatedCryptoTransform)_algorithm.CreateEncryptor(_cek, authenticatedEncryptionParameters.InitialVector, _authenticatedData);
            byte[] result = authenticatedCryptoTransform.TransformFinalBlock(plaintext, 0, plaintext.Length);
          //  byte[] result = _algorithm.CreateEncryptor(_cek, authenticatedEncryptionParameters.InitialVector, _authenticatedData).TransformFinalBlock(plaintext, 0, plaintext.Length);
            authenticatedEncryptionParameters.AuthenticationTag = authenticatedCryptoTransform.Tag;

            extraOutputs = authenticatedEncryptionParameters;

            return result;
        }

        public byte[] Decrypt(byte[] ciphertext)
        {
            if (ciphertext == null)
                throw LogHelper.LogArgumentNullException("ciphertext");

            if (ciphertext.Length == 0)
                throw LogHelper.LogException<ArgumentException>("Cannot encrypt empty 'ciphertext'");

            if (_authenticatedData == null)
                // TODO (Yan) : Add exception log message and throw;
                throw LogHelper.LogArgumentNullException("_authenticatedData");

            IAuthenticatedCryptoTransform authenticatedCryptoTransform = (IAuthenticatedCryptoTransform)_algorithm.CreateDecryptor(_authenticatedEncryptionParameters.Key,
                _authenticatedEncryptionParameters.InitialVector, _authenticatedData);
            byte[] result = authenticatedCryptoTransform.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            if (!authenticatedCryptoTransform.Tag.SequenceEqual(_authenticatedEncryptionParameters.AuthenticationTag))
                // TODO (Yan) : invalid AuthenticationData or Ciphertext, see https://tools.ietf.org/html/rfc7516#section-11.5 for security considerations on thwarting timing attachks.
                throw LogHelper.LogException<SecurityTokenValidationException>("Invalid AAD or CipherText.");

            return result;
        }

        private AesCbcHmacSha2 ResolveAlgorithm(string algorithm)
        {
            switch(algorithm)
            {
                case SecurityAlgorithms.Aes128CbcHmacSha256:
                    return new Aes128CbcHmacSha256();

                case SecurityAlgorithms.Aes256CbcHmacSha512:
                    return new Aes256CbcHmacSha512();

                default:
                    //TODO (Yan) : Add new exception to logMessages and throw;
                    throw LogHelper.LogArgumentException<ArgumentException>(nameof(algorithm), LogMessages.IDX10703);
            }
        }

        private byte[] GenerateCEK()
        {
            byte[] cek = null;
            return cek;
        }

        private byte[] GenerateIV()
        {
            byte[] iv = null;
            return iv;
        }

        private void ValidateKeySize(byte[] key, string algorithm)
        {
            switch (algorithm)
            {
                case SecurityAlgorithms.Aes128CbcHmacSha256:
                    {
                        if ((key.Length << 3) < 256)
                            // TODO (Yan) : Add new exception to LogMessages and throw;
                            throw LogHelper.LogArgumentException<ArgumentOutOfRangeException>("key.KeySize", LogMessages.IDX10630, key, algorithm, key.Length << 3);
                        break;
                    }
                    

                case SecurityAlgorithms.Aes256CbcHmacSha512:
                    {
                        if ((key.Length << 3) < 512)
                            // TODO (Yan) : Add new exception to LogMessages and throw;
                            throw LogHelper.LogArgumentException<ArgumentOutOfRangeException>("key.KeySize", LogMessages.IDX10630, key, algorithm, key.Length << 3);
                        break;
                    }                    

                default:
                    //TODO (Yan) : Add new exception to logMessages and throw;
                    throw LogHelper.LogArgumentException<ArgumentException>(nameof(algorithm), String.Format("Unsupported algorithm: {0}", algorithm));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }
    }
}

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;

namespace Progix.Modules.Fichiers.Infrastructure;

public class StorageOptions
{
    public const string Section = "Storage";
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "progix";
}

/// <summary>Stockage des contenus dans un service compatible S3 (MinIO en local).</summary>
public class Stockage
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public Stockage(IOptions<StorageOptions> options)
    {
        var o = options.Value;
        _bucket = o.Bucket;
        _client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey),
            new AmazonS3Config { ServiceURL = o.Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
    }

    public async Task PreparerAsync(CancellationToken ct)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucket))
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
    }

    public Task DeposerAsync(string cle, Stream contenu, string contentType, CancellationToken ct) =>
        _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = cle, InputStream = contenu, ContentType = contentType, AutoCloseStream = false, UseChunkEncoding = false,
        }, ct);

    // Envoi en plusieurs morceaux, pour les fichiers volumineux.
    public async Task<string> CommencerEnvoiAsync(string cle, string contentType, CancellationToken ct) =>
        (await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest { BucketName = _bucket, Key = cle, ContentType = contentType }, ct)).UploadId;

    public async Task<string> EnvoyerMorceauAsync(string cle, string envoiId, int numero, Stream contenu, CancellationToken ct) =>
        (await _client.UploadPartAsync(new UploadPartRequest
        {
            BucketName = _bucket, Key = cle, UploadId = envoiId, PartNumber = numero, InputStream = contenu, PartSize = contenu.Length, UseChunkEncoding = false,
        }, ct)).ETag;

    public Task TerminerEnvoiAsync(string cle, string envoiId, IEnumerable<(int Numero, string ETag)> morceaux, CancellationToken ct) =>
        _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = _bucket, Key = cle, UploadId = envoiId, PartETags = morceaux.OrderBy(m => m.Numero).Select(m => new PartETag(m.Numero, m.ETag)).ToList(),
        }, ct);

    public Task AbandonnerEnvoiAsync(string cle, string envoiId, CancellationToken ct) =>
        _client.AbortMultipartUploadAsync(_bucket, cle, envoiId, ct);

    public Task SupprimerAsync(string cle, CancellationToken ct) => _client.DeleteObjectAsync(_bucket, cle, ct);

    public async Task<Stream> LireAsync(string cle, CancellationToken ct) =>
        (await _client.GetObjectAsync(_bucket, cle, ct)).ResponseStream;
}

using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using System.IO.Compression;

namespace OHS.Copilot.Application.Services;

public class DocumentIngestService
{
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly ITextChunkingService _chunkingService;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<DocumentIngestService> _logger;

    public DocumentIngestService(
        IEnumerable<IDocumentParser> parsers,
        ITextChunkingService chunkingService,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ILogger<DocumentIngestService> logger)
    {
        _parsers = parsers;
        _chunkingService = chunkingService;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<IngestResponse> IngestAsync(IngestRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = new IngestResponse();

        _logger.LogInformation("Starting document ingest from: {Path}", request.DirectoryOrZipPath);

        try
        {
            var filePaths = GetFilesToProcess(request);
            response.ProcessedFiles = filePaths.Count;

            var chunkingOptions = new ChunkingOptions
            {
                ChunkSize = request.ChunkSize,
                ChunkOverlap = request.ChunkOverlap
            };

            var allChunks = new List<Chunk>();
            var existingHashes = new HashSet<string>();

            if (!request.RebuildIndex)
            {
                existingHashes = await GetExistingChunkHashesAsync(cancellationToken);
                _logger.LogDebug("Found {Count} existing chunk hashes", existingHashes.Count);
            }

            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new ProcessedFileInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath
                };

                try
                {
                    var chunks = await ProcessFileAsync(filePath, chunkingOptions, existingHashes, cancellationToken);
                    
                    fileInfo.ChunkCount = chunks.Count;
                    fileInfo.Status = "Success";
                    
                    allChunks.AddRange(chunks);
                    response.GeneratedChunks += chunks.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);
                    fileInfo.Status = "Failed";
                    fileInfo.ErrorMessage = ex.Message;
                    response.ErrorMessages.Add($"File {filePath}: {ex.Message}");
                }

                response.FileDetails.Add(fileInfo);
            }

            response.UniqueHashes = allChunks.Select(c => c.Hash).Distinct().Count();
            response.SkippedDuplicates = response.GeneratedChunks - response.UniqueHashes;

            if (allChunks.Count > 0)
            {
                await StoreChunksAsync(allChunks, cancellationToken);
                _logger.LogInformation("Successfully stored {ChunkCount} chunks", allChunks.Count);
            }

            stopwatch.Stop();
            response.ProcessingTime = stopwatch.Elapsed;

            _logger.LogInformation("Document ingest completed: {ProcessedFiles} files, {GeneratedChunks} chunks, {ProcessingTime}ms",
                response.ProcessedFiles, response.GeneratedChunks, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            response.ProcessingTime = stopwatch.Elapsed;
            response.ErrorMessages.Add($"Ingest failed: {ex.Message}");
            
            _logger.LogError(ex, "Document ingest failed");
            
            return response;
        }
    }

    private List<string> GetFilesToProcess(IngestRequest request)
    {
        var filePaths = new List<string>();

        try
        {
            if (request.DirectoryOrZipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                filePaths = ExtractZipFile(request.DirectoryOrZipPath, request.SupportedExtensions);
            }
            else if (Directory.Exists(request.DirectoryOrZipPath))
            {
                filePaths = GetFilesFromDirectory(request.DirectoryOrZipPath, request.SupportedExtensions);
            }
            else if (File.Exists(request.DirectoryOrZipPath))
            {
                filePaths = [request.DirectoryOrZipPath];
            }
            else
            {
                _logger.LogWarning("Path not found: {Path}", request.DirectoryOrZipPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files from path: {Path}", request.DirectoryOrZipPath);
        }

        return filePaths;
    }

    private List<string> GetFilesFromDirectory(string directoryPath, List<string> supportedExtensions)
    {
        var files = new List<string>();

        foreach (var extension in supportedExtensions)
        {
            var pattern = $"*{extension}";
            files.AddRange(Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories));
        }

        _logger.LogDebug("Found {FileCount} files in directory: {Directory}", files.Count, directoryPath);
        return files;
    }

    private List<string> ExtractZipFile(string zipPath, List<string> supportedExtensions)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            
            var extractedFiles = GetFilesFromDirectory(tempDir, supportedExtensions);
            
            _logger.LogDebug("Extracted {FileCount} files from ZIP: {ZipPath}", extractedFiles.Count, zipPath);
            
            return extractedFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract ZIP file: {ZipPath}", zipPath);
            
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            
            throw;
        }
    }

    private async Task<List<Chunk>> ProcessFileAsync(
        string filePath, 
        ChunkingOptions chunkingOptions, 
        HashSet<string> existingHashes,
        CancellationToken cancellationToken)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(filePath));
        
        if (parser == null)
        {
            throw new NotSupportedException($"No parser available for file: {filePath}");
        }

        var parseResult = await parser.ParseAsync(filePath, cancellationToken);
        
        if (!parseResult.Success)
        {
            throw new InvalidOperationException($"Failed to parse file: {parseResult.ErrorMessage}");
        }

        var chunks = _chunkingService.ChunkDocument(parseResult, chunkingOptions);
        
        foreach (var chunk in chunks)
        {
            chunk.SourcePath = filePath;
        }

        var newChunks = chunks.Where(c => !existingHashes.Contains(c.Hash)).ToList();
        
        _logger.LogDebug("File {FileName}: {TotalChunks} chunks, {NewChunks} new chunks",
            Path.GetFileName(filePath), chunks.Count, newChunks.Count);

        return newChunks;
    }

    private async Task<HashSet<string>> GetExistingChunkHashesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _vectorStore.GetCountAsync(cancellationToken);
            _logger.LogDebug("Vector store contains {Count} existing chunks", count);
            
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get existing chunk hashes, assuming empty store");
            return [];
        }
    }

    private async Task StoreChunksAsync(List<Chunk> chunks, CancellationToken cancellationToken)
    {
        const int batchSize = 10;
        var batches = chunks.Select((chunk, index) => new { chunk, index })
                           .GroupBy(x => x.index / batchSize)
                           .Select(g => g.Select(x => x.chunk).ToList())
                           .ToList();

        _logger.LogDebug("Storing chunks in {BatchCount} batches of {BatchSize}", batches.Count, batchSize);

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    batch.Select(c => c.Text), 
                    cancellationToken);

                var batchItems = batch.Zip(embeddings, (chunk, embedding) => (chunk, embedding));
                
                await _vectorStore.UpsertBatchAsync(batchItems, cancellationToken);
                
                _logger.LogDebug("Stored batch of {ChunkCount} chunks", batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store batch of {ChunkCount} chunks", batch.Count);
                
                foreach (var chunk in batch)
                {
                    try
                    {
                        var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Text, cancellationToken);
                        await _vectorStore.UpsertAsync(chunk, embedding, cancellationToken);
                    }
                    catch (Exception chunkEx)
                    {
                        _logger.LogWarning(chunkEx, "Failed to store individual chunk {ChunkId}", chunk.Id);
                    }
                }
            }
        }
    }
}

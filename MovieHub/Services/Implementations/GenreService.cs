using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MovieHub.Data;
using MovieHub.Data.Dtos.Genre;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Interfaces;

namespace MovieHub.Services.Implementations;

public class GenreService : IGenreService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;

    private const string GenresCacheKey = "genres:all";

    public GenreService(ApplicationDbContext context, IMapper mapper, IMemoryCache cache)
    {
        _context = context;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<IEnumerable<GenreResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(GenresCacheKey, out IEnumerable<GenreResponseDto>? cached) && cached != null)
            return cached;

        var genres = await _context.Genres.OrderBy(g => g.Name).ToListAsync(cancellationToken);
        var dto = _mapper.Map<IEnumerable<GenreResponseDto>>(genres);

        _cache.Set(GenresCacheKey, dto, TimeSpan.FromMinutes(10)); // gêneros mudam pouco

        return dto;
    }

    public async Task<GenreResponseDto> CreateAsync(GenreRequestDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Genres.AnyAsync(g => g.Name == dto.Name, cancellationToken);
        if (exists)
            throw new ConflictException($"Gênero '{dto.Name}' já existe.");

        var genre = _mapper.Map<Genre>(dto);
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(GenresCacheKey);
        return _mapper.Map<GenreResponseDto>(genre);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var genre = await _context.Genres.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Gênero com id {id} não encontrado.");

        _context.Genres.Remove(genre);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(GenresCacheKey);
    }
}


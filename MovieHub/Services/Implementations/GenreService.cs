using AutoMapper;
using Microsoft.EntityFrameworkCore;
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

    public GenreService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<GenreResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var genres = await _context.Genres.OrderBy(g => g.Name).ToListAsync(cancellationToken);
        return _mapper.Map<IEnumerable<GenreResponseDto>>(genres);
    }

    public async Task<GenreResponseDto> CreateAsync(GenreRequestDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Genres.AnyAsync(g => g.Name == dto.Name, cancellationToken);
        if (exists)
            throw new ConflictException($"Gênero '{dto.Name}' já existe.");

        var genre = _mapper.Map<Genre>(dto);
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync(cancellationToken);
        return _mapper.Map<GenreResponseDto>(genre);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var genre = await _context.Genres.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Gênero com id {id} não encontrado.");

        _context.Genres.Remove(genre);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

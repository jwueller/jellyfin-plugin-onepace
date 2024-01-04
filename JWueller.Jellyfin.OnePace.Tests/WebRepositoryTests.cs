using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace JWueller.Jellyfin.OnePace.Tests;

public class WebRepositoryTests
{
    // language=JSON
    private const string MetadataResponse = """
        {
            "data": {
                "series": {
                      "invariant_title": "One Pace",
                      "translations": [
                            {
                                  "title": "One Pace en",
                                  "description": "English description",
                                  "language_code": "en"
                            },
                            {
                                  "title": "One Pace de",
                                  "description": "Deutsche Beschreibung",
                                  "language_code": "de"
                            }
                      ]
                },

                "arcs": [
                    {
                        "id": "clksypeix000008jw066ye7lo",
                        "part": 1,
                        "invariant_title": "Romance Dawn",
                        "manga_chapters": "1-7",
                        "released_at": "2020-12-02T12:00:00Z",
                        "translations": [
                            {
                                "title": "Romance Dawn en",
                                "description": "English description for Romance Dawn",
                                "language_code": "en"
                            }
                        ],
                        "images": [
                            {
                                "src": "cover-romance-dawn-arc_270w.jpg",
                                "width": "270"
                            },
                            {
                                "src": "cover-romance-dawn-arc_135w.webp",
                                "width": "135"
                            },
                            {
                                "src": "cover-romance-dawn-arc_270w.webp",
                                "width": "270"
                            },
                            {
                                "src": "cover-romance-dawn-arc_405w.webp",
                                "width": "405"
                            }
                        ],
                        "episodes": [
                            {
                                "id": "clksyqwxl000208jw82wh3y0g",
                                "part": 1,
                                "invariant_title": "Romance Dawn 01",
                                "manga_chapters": "1",
                                "released_at": "2020-12-02T12:00:00Z",
                                "crc32": "11000000",
                                "translations": [
                                    {
                                        "title": "Romance Dawn 01 de",
                                        "description": "Deutsche Beschreibung für Romance Dawn 01",
                                        "language_code": "de"
                                    },
                                    {
                                        "title": "Romance Dawn 01 en",
                                        "description": "English description for Romance Dawn 01",
                                        "language_code": "en"
                                    }
                                ],
                                "images": [
                                    {
                                        "src": "cover-romance-dawn-01_480w.jpg",
                                        "width": "480"
                                    },
                                    {
                                        "src": "cover-romance-dawn-01_240w.webp",
                                        "width": "240"
                                    },
                                    {
                                        "src": "cover-romance-dawn-01_480w.webp",
                                        "width": "480"
                                    }
                                ]
                            },
                            {
                                "id": "clksys3c2000308jwa08325o7",
                                "part": 2,
                                "invariant_title": "Romance Dawn 02",
                                "manga_chapters": "2",
                                "released_at": "2020-12-02T12:00:00Z",
                                "crc32": "12000000",
                                "translations": [
                                    {
                                        "title": "Romance Dawn 02 de",
                                        "description": "Deutsche Beschreibung für Romance Dawn 02",
                                        "language_code": "de"
                                    },
                                    {
                                        "title": "Romance Dawn 02 en",
                                        "description": "English description for Romance Dawn 02",
                                        "language_code": "en"
                                    }
                                ],
                                "images": [
                                    {
                                        "src": "cover-romance-dawn-02_480w.jpg",
                                        "width": "480"
                                    }
                                ]
                            },
                            {
                                "id": "clksysvim000408jw6anzden8",
                                "part": 3,
                                "invariant_title": "Romance Dawn 03",
                                "manga_chapters": null,
                                "released_at": null,
                                "crc32": null,
                                "translations": [],
                                "images": []
                            }
                        ]
                    },
                    {
                        "id": "clksyq4q5000108jwgihd6jud",
                        "part": 2,
                        "invariant_title": "Orange Town",
                        "manga_chapters": "8-21",
                        "released_at": "2021-08-07T12:00:00Z",
                        "translations": [
                            {
                                "title": "Orange Town de",
                                "description": "Deutsche Beschreibung für Orange Town",
                                "language_code": "de"
                            },
                            {
                                "title": "Orange Town en",
                                "description": "English description for Orange Town",
                                "language_code": "en"
                            }
                        ],
                        "images": [
                            {
                                "src": "cover-orange-town-arc_270w.jpg",
                                "width": "270"
                            }
                        ],
                        "episodes": [
                            {
                                "id": "clksytlbt000508jw6r9x1jb1",
                                "part": 1,
                                "invariant_title": "Orange Town 01",
                                "manga_chapters": "8-11",
                                "released_at": "2021-08-07T12:00:00Z",
                                "crc32": "21000000",
                                "translations": [
                                    {
                                        "title": "Orange Town 01 de",
                                        "description": "Deutsche Beschreibung für Orange Town 01",
                                        "language_code": "de"
                                    },
                                    {
                                        "title": "Orange Town 01 en",
                                        "description": "English description for Orange Town 01",
                                        "language_code": "en"
                                    }
                                ],
                                "images": [
                                    {
                                        "src": "cover-orange-town-01_480w.jpg",
                                        "width": "480"
                                    },
                                    {
                                        "src": "cover-orange-town-01_240w.webp",
                                        "width": "240"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        }
    """;

    private readonly WebRepository _webRepository;

    public WebRepositoryTests()
    {
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                if (request.RequestUri != null &&
                    request.Method == HttpMethod.Post &&
                    request.RequestUri.AbsoluteUri == "https://onepace.net/api/graphql" &&
                    request.Content != null)
                {
                    var requestContent = request.Content.ReadAsStringAsync(cancellationToken).Result;
                    if (requestContent.Contains("series") && requestContent.Contains("arcs"))
                    {
                        return Task.FromResult(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(MetadataResponse)
                        });
                    }
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });
            });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(httpMessageHandlerMock.Object));

        _webRepository = new WebRepository(httpClientFactoryMock.Object, new MemoryCache(new MemoryCacheOptions()),
            NullLogger<WebRepository>.Instance);
    }

    [Fact]
    public async void ShouldFindSeries()
    {
        var result = await _webRepository.FindSeriesAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("One Pace", result.InvariantTitle);
    }

    [Fact]
    public async void ShouldFindAllArcs()
    {
        var result = await _webRepository.FindAllArcsAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Collection(result,
            arc => Assert.Equal("Romance Dawn", arc.InvariantTitle),
            arc => Assert.Equal("Orange Town", arc.InvariantTitle));
    }

    [Fact]
    public async void ShouldFindAllEpisodes()
    {
        var result = await _webRepository.FindAllEpisodesAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Collection(result,
            episode => Assert.Equal("Romance Dawn 01", episode.InvariantTitle),
            episode => Assert.Equal("Romance Dawn 02", episode.InvariantTitle),
            episode => Assert.Equal("Romance Dawn 03", episode.InvariantTitle),
            episode => Assert.Equal("Orange Town 01", episode.InvariantTitle));
    }

    [Theory]
    [InlineData("clksypeix000008jw066ye7lo", "Romance Dawn")]
    [InlineData("clksyq4q5000108jwgihd6jud", "Orange Town")]
    public async void ShouldFindArcById(string arcId, string expectedInvariantTitle)
    {
        var result = await _webRepository.FindArcByIdAsync(arcId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedInvariantTitle, result.InvariantTitle);
    }

    [Theory]
    [InlineData("clksyqwxl000208jw82wh3y0g", "Romance Dawn 01")]
    [InlineData("clksys3c2000308jwa08325o7", "Romance Dawn 02")]
    [InlineData("clksysvim000408jw6anzden8", "Romance Dawn 03")]
    [InlineData("clksytlbt000508jw6r9x1jb1", "Orange Town 01")]
    public async void ShouldFindEpisodeById(string episodeId, string expectedInvariantTitle)
    {
        var result = await _webRepository.FindEpisodeByIdAsync(episodeId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedInvariantTitle, result.InvariantTitle);
    }

    /// <summary>
    /// Regression test for ArcId not being populated correctly.
    /// </summary>
    /// <see href="https://github.com/jwueller/jellyfin-plugin-onepace/issues/12"/>
    /// <see href="https://github.com/jwueller/jellyfin-plugin-onepace/issues/28"/>
    [Fact]
    public async void ShouldFindEpisodeWithMatchingArcById()
    {
        var result = await _webRepository.FindEpisodeByIdAsync("clksyqwxl000208jw82wh3y0g", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("clksyqwxl000208jw82wh3y0g", result.Id);
        Assert.Equal("clksypeix000008jw066ye7lo", result.ArcId);
    }

    [Theory]
    [InlineData("en", "One Pace en", "English description")]
    [InlineData("de", "One Pace de", "Deutsche Beschreibung")]
    [InlineData("invalid", "One Pace en", "English description")] // fallback
    public async void ShouldFindBestSeriesLocalization(string languageCode, string expectedTitle,
        string expectedDescription)
    {
        var result = await _webRepository.FindBestLocalizationBySeriesAsync(languageCode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedDescription, result.Description);
    }

    [Theory]
    [InlineData("clksypeix000008jw066ye7lo", "en", "Romance Dawn en", "English description for Romance Dawn")]
    [InlineData("clksyq4q5000108jwgihd6jud", "en", "Orange Town en", "English description for Orange Town")]
    [InlineData("clksyq4q5000108jwgihd6jud", "de", "Orange Town de", "Deutsche Beschreibung für Orange Town")]
    [InlineData("clksyq4q5000108jwgihd6jud", "invalid", "Orange Town en", "English description for Orange Town")] // fallback
    public async void ShouldFindBestArcLocalization(
        string arcId,
        string languageCode,
        string expectedTitle,
        string expectedDescription)
    {
        var result = await _webRepository.FindBestLocalizationByArcIdAsync(arcId, languageCode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedDescription, result.Description);
    }

    [Theory]
    [InlineData("clksyqwxl000208jw82wh3y0g", "de", "Romance Dawn 01 de", "Deutsche Beschreibung für Romance Dawn 01")]
    [InlineData("clksyqwxl000208jw82wh3y0g", "en", "Romance Dawn 01 en", "English description for Romance Dawn 01")]
    [InlineData("clksyqwxl000208jw82wh3y0g", "invalid", "Romance Dawn 01 en", "English description for Romance Dawn 01")] // fallback
    public async void ShouldFindBestEpisodeLocalization(
        string episodeId,
        string languageCode,
        string expectedTitle,
        string expectedDescription)
    {
        var result = await _webRepository.FindBestLocalizationByEpisodeIdAsync(
            episodeId, languageCode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedDescription, result.Description);
    }

    [Fact]
    public async void ShouldFindSeriesLogoArt()
    {
        var result = await _webRepository.FindAllLogoArtBySeriesAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async void ShouldFindSeriesCoverArt()
    {
        var result = await _webRepository.FindAllCoverArtBySeriesAsync(CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("clksypeix000008jw066ye7lo", 4)]
    [InlineData("clksyq4q5000108jwgihd6jud", 1)]
    public async void ShouldFindAllArcCoverArt(string arcId, int expectedCoverArtCount)
    {
        var result = await _webRepository.FindAllCoverArtByArcIdAsync(arcId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(result.Count, expectedCoverArtCount);
    }

    [Theory]
    [InlineData("clksyqwxl000208jw82wh3y0g", 3)]
    [InlineData("clksys3c2000308jwa08325o7", 1)]
    [InlineData("clksytlbt000508jw6r9x1jb1", 2)]
    public async void ShouldFindAllEpisodeCoverArt(string episodeId, int expectedCoverArtCount)
    {
        var result = await _webRepository.FindAllCoverArtByEpisodeIdAsync(episodeId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(result.Count, expectedCoverArtCount);
    }
}

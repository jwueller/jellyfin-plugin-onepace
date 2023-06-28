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
    private const string MetadataEnResponse = """
        {
            "meta-title": "One Pace | english tagline",
            "meta-description": "English description"
        }
    """;

    // language=JSON
    private const string MetadataDeResponse = """
        {
            "meta-title": "One Pace | Deutscher Slogan",
            "meta-description": "Deutsche Beschreibung"
        }
    """;

    // language=JSON
    private const string ContentResponse = """
        {
            "data": {
                "databaseGetAllArcs": [
                    {
                        "part": 1,
                        "title": "Romance Dawn",
                        "manga_chapters": "1-7",
                        "released_date": "2020-12-02T12:00:00Z",
                        "translations": [
                            {
                                "title": "Romance Dawn en",
                                "description": "English description for Romance Dawn",
                                "language": {
                                    "code": "en"
                                }
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
                                "part": 1,
                                "title": "Romance Dawn 01",
                                "manga_chapters": "1",
                                "released_date": "2020-12-02T12:00:00Z",
                                "translations": [
                                    {
                                        "title": "Romance Dawn 01 de",
                                        "description": "Deutsche Beschreibung für Romance Dawn 01",
                                        "language": {
                                            "code": "de"
                                        }
                                    },
                                    {
                                        "title": "Romance Dawn 01 en",
                                        "description": "English description for Romance Dawn 01",
                                        "language": {
                                            "code": "en"
                                        }
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
                                "part": 2,
                                "title": "Romance Dawn 02",
                                "manga_chapters": "2",
                                "released_date": "2020-12-02T12:00:00Z",
                                "translations": [
                                    {
                                        "title": "Romance Dawn 02 de",
                                        "description": "Deutsche Beschreibung für Romance Dawn 02",
                                        "language": {
                                            "code": "de"
                                        }
                                    },
                                    {
                                        "title": "Romance Dawn 02 en",
                                        "description": "English description for Romance Dawn 02",
                                        "language": {
                                            "code": "en"
                                        }
                                    }
                                ],
                                "images": [
                                    {
                                        "src": "cover-romance-dawn-02_480w.jpg",
                                        "width": "480"
                                    }
                                ]
                            }
                        ]
                    },
                    {
                        "part": 2,
                        "title": "Orange Town",
                        "manga_chapters": "8-21",
                        "released_date": "2021-08-07T12:00:00Z",
                        "translations": [
                            {
                                "title": "Orange Town de",
                                "description": "Deutsche Beschreibung für Orange Town",
                                "language": {
                                    "code": "de"
                                }
                            },
                            {
                                "title": "Orange Town en",
                                "description": "English description for Orange Town",
                                "language": {
                                    "code": "en"
                                }
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
                                "part": 1,
                                "title": "Orange Town 01",
                                "manga_chapters": "8-11",
                                "released_date": "2021-08-07T12:00:00Z",
                                "translations": [
                                    {
                                        "title": "Orange Town 01 de",
                                        "description": "Deutsche Beschreibung für Orange Town 01",
                                        "language": {
                                            "code": "de"
                                        }
                                    },
                                    {
                                        "title": "Orange Town 01 en",
                                        "description": "English description for Orange Town 01",
                                        "language": {
                                            "code": "en"
                                        }
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
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken _) =>
            {
                if (request.RequestUri != null && request.Method == HttpMethod.Get)
                {
                    if (request.RequestUri.AbsoluteUri == "https://onepace.net/static/locales/en/home.json")
                    {
                        return Task.FromResult(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(MetadataEnResponse),
                        });
                    }

                    if (request.RequestUri.AbsoluteUri == "https://onepace.net/static/locales/de/home.json")
                    {
                        return Task.FromResult(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(MetadataDeResponse),
                        });
                    }
                }

                if (request.RequestUri != null && request.Method == HttpMethod.Post)
                {
                    if (request.RequestUri.AbsoluteUri.StartsWith("https://onepace.net/api/graphql") && request.Content != null && request.Content.ReadAsStringAsync(_).Result.Contains("databaseGetAllArcs"))
                    {
                        return Task.FromResult(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(ContentResponse),
                        });
                    }
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                });
            });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(httpMessageHandlerMock.Object));

        _webRepository = new WebRepository(httpClientFactoryMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<WebRepository>.Instance);
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
            episode => Assert.Equal("Orange Town 01", episode.InvariantTitle));
    }

    [Theory]
    [InlineData(1, "Romance Dawn")]
    [InlineData(2, "Orange Town")]
    public async void ShouldFindArcByNumber(int arcNumber, string expectedInvariantTitle)
    {
        var result = await _webRepository.FindArcByNumberAsync(arcNumber, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedInvariantTitle, result.InvariantTitle);
    }

    [Theory]
    [InlineData(1, 1, "Romance Dawn 01")]
    [InlineData(1, 2, "Romance Dawn 02")]
    [InlineData(2, 1, "Orange Town 01")]
    public async void ShouldFindEpisodeByNumber(int arcNumber, int episodeNumber, string expectedInvariantTitle)
    {
        var result = await _webRepository.FindEpisodeByNumberAsync(arcNumber, episodeNumber, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedInvariantTitle, result.InvariantTitle);
    }

    [Theory]
    [InlineData("en", "One Pace", "English description")]
    [InlineData("de", "One Pace", "Deutsche Beschreibung")]
    [InlineData("invalid", "One Pace", "English description")] // fallback
    public async void ShouldFindBestSeriesLocalization(string languageCode, string expectedTitle, string expectedDescription)
    {
        var result = await _webRepository.FindBestSeriesLocalizationAsync(languageCode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedDescription, result.Description);
    }

    [Theory]
    [InlineData(1, "en", "Romance Dawn en", "English description for Romance Dawn")]
    [InlineData(2, "en", "Orange Town en", "English description for Orange Town")]
    [InlineData(2, "de", "Orange Town de", "Deutsche Beschreibung für Orange Town")]
    [InlineData(2, "invalid", "Orange Town en", "English description for Orange Town")] // fallback
    public async void ShouldFindBestArcLocalization(int arcNumber, string languageCode, string expectedTitle, string expectedDescription)
    {
        var result = await _webRepository.FindBestArcLocalizationAsync(arcNumber, languageCode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedDescription, result.Description);
    }

    [Theory]
    [InlineData(1, 1, "de", "Romance Dawn 01 de", "Deutsche Beschreibung für Romance Dawn 01")]
    [InlineData(1, 1, "en", "Romance Dawn 01 en", "English description for Romance Dawn 01")]
    [InlineData(1, 1, "invalid", "Romance Dawn 01 en", "English description for Romance Dawn 01")] // fallback
    public async void ShouldFindBestEpisodeLocalization(int arcNumber, int episodeNumber, string languageCode, string expectedTitle, string expectedDescription)
    {
        var result = await _webRepository.FindBestEpisodeLocalizationAsync(arcNumber, episodeNumber, languageCode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedDescription, result.Description);
    }

    [Fact]
    public async void ShouldFindSeriesLogoArt()
    {
        var result = await _webRepository.FindAllSeriesLogoArtAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async void ShouldFindSeriesCoverArt()
    {
        var result = await _webRepository.FindAllSeriesCoverArtAsync(CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 1)]
    public async void ShouldFindAllArcCoverArt(int arcNumber, int expectedCoverArtCount)
    {
        var result = await _webRepository.FindAllArcCoverArtAsync(arcNumber, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(result.Count, expectedCoverArtCount);
    }

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 1, 2)]
    public async void ShouldFindAllEpisodeCoverArt(int arcNumber, int episodeNumber, int expectedCoverArtCount)
    {
        var result = await _webRepository.FindAllEpisodeCoverArtAsync(arcNumber, episodeNumber, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(result.Count, expectedCoverArtCount);
    }

}

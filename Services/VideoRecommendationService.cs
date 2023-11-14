using Instances;
using MathNet.Numerics;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using System.Linq;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Services
{
    public class VideoRecommendationService
    {
        private ApplicationDbContext? Context { get; }

        public VideoRecommendationService(ApplicationDbContext context)
        {
            Context = context;
        }

        public List<Video> GetRecommendedVideos(string userId)
        {
            try
            {
                var user = Context.Users
                    .Include(u => u.VideoViews)
                    .ThenInclude(vv => vv.Video)
                    .ThenInclude(v => v.AssociatedVideoTags)
                    .ThenInclude(avt => avt.Tag)
                    .Where(u => u.Id.ToString() == userId).FirstOrDefault();

                if (user.VideoViews.Count < 2)
                {
                    throw new Exception();
                }

                var userTitles = user.VideoViews
                                    .OrderByDescending(v => v.ViewCount)
                                    .Select(vv => vv.Video)
                                    .Select(v => v.Title)
                                    .Distinct()
                                    .ToList();

                var userAssociatedTags = user.VideoViews
                                    .OrderByDescending(v => v.ViewCount)
                                    .Select(vv => vv.Video)
                                    .Select(v => v.AssociatedVideoTags)
                                    .Distinct()
                                    .ToList();

                var userTags = Context.Tags
                    .Where(t => t.AssociatedVideoTags
                        .Any(avt => userAssociatedTags
                            .Any(uat => uat == avt)))
                    .Select(t => t.Name)
                    .Distinct().ToList();

                var similarVideos = Context.Videos
                    .Where(v => v.IsPublished == true)
                    .Include(v => v.Channel)
                    .Include(v => v.VideoViews)
                    .Include(v => v.AssociatedVideoTags)
                    .ThenInclude(avt => avt.Tag)
                    .Where(video => IsTitleOrTagsSimilar(userTitles, userTags, video))
                    .Distinct().ToList();

                var unwatchedVideos = similarVideos
                    .Where(video => !user.VideoViews
                        .Any(vv => vv.Id == video.Id))
                    .ToList();

                var rankedVideos = unwatchedVideos
                    .OrderByDescending(video => CalculateSimilarity(userTags, video))
                    .ToList();

                if (rankedVideos.Count < 2)
                {
                    throw new Exception();
                }

                var recommendations = rankedVideos
                    .OrderByDescending(CalculatePopularity)
                    .ThenBy(video => IsSubscribed(user, video))
                    .ToList();

                return recommendations.Take(10).ToList();

            }
            catch (Exception ex)
            {
                return Context.Videos
                    .Where(v => v.IsPublished == true)
                    .Include(v => v.Channel)
                    .OrderByDescending(CalculatePopularity)
                    .Take(10)
                    .ToList();
            }
        }

        private bool IsTitleOrTagsSimilar(List<string> userTitles, List<string> userTags, Video video)
        {
            return userTitles.Any(title =>
                title.Equals(video.Title, StringComparison.OrdinalIgnoreCase)) ||
                userTags.Any(userTag =>
                    video.AssociatedVideoTags.Any(avt => 
                        avt.Tag.Name.Equals(userTag, StringComparison.OrdinalIgnoreCase)));
        }

        private double CalculateSimilarity(List<string> userTags, Video video)
        {
            var videoTags = video.AssociatedVideoTags
                .Select(avt => avt.Tag)
                .Select(t => t.Name)
                .Distinct().ToList();

            double tagSimilarity = CalculateEuclideanDistance(userTags, videoTags);

            return tagSimilarity;
        }

        private double CalculatePopularity(Video video)
        {
            int totalViewCount = video.VideoViews.Sum(vv => vv.ViewCount);

            return totalViewCount;
        }

        public bool IsSubscribed(User user, Video video)
        {
            var channel = Context.Channels
                .Include(c => c.FollowingUserChannels)
                .Where(c => c.Id == video.Channel.Id)
                .FirstOrDefault();

            bool isChannelFollowed = channel.FollowingUserChannels
                .Any(fc => fc.UserId == user.Id);

            return isChannelFollowed;
        }

        private double CalculateEuclideanDistance(List<string> userTags, List<string> videoTags)
        {
            var userVector = ConvertToVector(userTags);
            var videoVector = ConvertToVector(videoTags);

            var distance = Distance.Euclidean(userVector, videoVector);

            var similarityScore = 1.0 / (1.0 + distance);

            return similarityScore;
        }

        private double[] ConvertToVector(List<string> tags)
        {
            var allTags = tags.Distinct().ToList();
            var vector = new double[allTags.Count];

            for (int i = 0; i < allTags.Count; i++)
            {
                vector[i] = tags.Count(tag => tag == allTags[i]);
            }

            return vector;
        }
    }
}

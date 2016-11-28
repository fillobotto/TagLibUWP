﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Xunit;

namespace TagLibUWP.Test
{
    public class TagManagerTest
    {
        private static string[] UnsupportedAudioExtensions = new string[] { "aac", "txt" };
        public static IEnumerable<object[]> UnsupportedAudioFileNames { get { return UnsupportedAudioExtensions.Select(d => new object[] { "Unsupported." + d }); } }

        private static string[] SupportedAudioExtensions = new string[] { "asf", "flac", "m4a", "m4v", "mp3", "mp4", "ogg", "wma", "wav", "wv" };
        public static IEnumerable<object[]> SupportedAudioFileNames { get { return SupportedAudioExtensions.Select(d => new object[] { "AudioTest." + d }); } }

        [Fact(DisplayName = "Getting supported extensions")]
        public void GetSupportedExtensionsWorks()
        {
            var extensions = TagManager.SupportedExtensions;
            Assert.NotNull(extensions);
            Assert.NotEmpty(extensions);
        }

        [Theory(DisplayName = "Unsupported files handling"), MemberData(nameof(UnsupportedAudioFileNames))]
        public async Task UnsupportedFilesHandlingWorks(string fileName)
        {
            var file = await GetTestMediaFileAsync(fileName);

            try
            {
                var tag = TagManager.ReadFile(file);
                Assert.True(false);
            }
            catch(COMException e)
            {
                Assert.NotNull(e);
            }
        }

        [Theory(DisplayName = "Tag reading"), MemberData(nameof(SupportedAudioFileNames))]
        public async Task TagReadingWorks(string fileName)
        {
            var file = await GetTestMediaFileAsync(fileName);

            var fileInfo = await Task.Run(() => TagManager.ReadFile(file));
            Assert.Same(file, fileInfo.File);
            Assert.NotNull(fileInfo.Properties);

            var tag = fileInfo.Tag;
            Assert.NotNull(tag);
            Assert.Equal(nameof(tag.Album), tag.Album);
            Assert.Equal(nameof(tag.Artist), tag.Artist);
            Assert.Equal(nameof(tag.Comment), tag.Comment);
            Assert.Equal(nameof(tag.Genre), tag.Genre);
            Assert.Equal(nameof(tag.Title) + "あア亜", tag.Title);
            Assert.Equal(45U, tag.Track);
            Assert.Equal(2000U, tag.Year);

            var image = tag.Image;
            Assert.NotNull(image);
            Assert.Equal("image/jpeg", image.MIMEType);
            Assert.NotNull(image.Bytes);
            Assert.Equal(151308, image.Bytes.Length);
        }

        [Theory(DisplayName = "Longer tag writing"), MemberData(nameof(SupportedAudioFileNames))]
        public Task LongerTagWritingWorks(string fileName)
        {
            return TagWritingTest(fileName, d => d + "-suffix");
        }

        [Theory(DisplayName = "Same length tag writing"), MemberData(nameof(SupportedAudioFileNames))]
        public Task SameLengthTagWritingWorks(string fileName)
        {
            return TagWritingTest(fileName, d => d.Substring(2) + "zz");
        }

        [Theory(DisplayName = "Shorter tag writing"), MemberData(nameof(SupportedAudioFileNames))]
        public Task ShorterTagWritingWorks(string fileName)
        {
            return TagWritingTest(fileName, d => d.Substring(2));
        }

        [Theory(DisplayName = "Empty tag writing"), MemberData(nameof(SupportedAudioFileNames))]
        public Task EmptyTagWritingWorks(string fileName)
        {
            return TagWritingTest(fileName, d => string.Empty);
        }

        public async Task TagWritingTest(string fileName, Func<string, string> TagTransformation)
        {
            var file = await GetTestMediaFileAsync(fileName);
            file = await CopyToTempFileAsync(file);

            var fileInfo = await Task.Run(() => TagManager.ReadFile(file));

            var tag = fileInfo.Tag;
            tag.Album = TagTransformation(nameof(tag.Album));
            tag.Artist = TagTransformation(nameof(tag.Artist));
            tag.Genre = TagTransformation(nameof(tag.Genre));
            tag.Title = TagTransformation(nameof(tag.Title) + "あア亜");
            tag.Comment = TagTransformation(nameof(tag.Comment));
            var numBase = 22U;
            tag.Track = numBase + 1;
            tag.Year = numBase + 3;

            await Task.Run(() => TagManager.WriteFile(file, tag));

            fileInfo = await Task.Run(() => TagManager.ReadFile(file));
            tag = fileInfo.Tag;
            Assert.Equal(TagTransformation(nameof(tag.Album)), tag.Album);
            Assert.Equal(TagTransformation(nameof(tag.Artist)), tag.Artist);
            Assert.Equal(TagTransformation(nameof(tag.Genre)), tag.Genre);
            Assert.Equal(TagTransformation(nameof(tag.Title) + "あア亜"), tag.Title);
            Assert.Equal(TagTransformation(nameof(tag.Comment)), tag.Comment);
            Assert.Equal(numBase + 1, tag.Track);
            Assert.Equal(numBase + 3, tag.Year);

            await file.DeleteAsync();
        }

        [Theory(DisplayName = "Image update"), MemberData(nameof(SupportedAudioFileNames))]
        public async Task ImageUpdateTest(string fileName)
        {
            var file = await GetTestMediaFileAsync(fileName);
            file = await CopyToTempFileAsync(file);

            var fileInfo = await Task.Run(() => TagManager.ReadFile(file));

            var testImageMIME = "image/png";
            var testImageBytes = new byte[] { 1, 2, 3, 4, 5 };

            var image = fileInfo.Tag.Image;
            image.MIMEType = testImageMIME;
            image.Bytes = testImageBytes;

            await Task.Run(() => TagManager.WriteFile(file, fileInfo.Tag));

            fileInfo = await Task.Run(() => TagManager.ReadFile(file));
            image = fileInfo.Tag.Image;

            Assert.Equal(testImageMIME, image.MIMEType);
            Assert.Equal(testImageBytes.Length, image.Bytes.Length);
            for (var i = 0; i < testImageBytes.Length; i++)
            {
                Assert.Equal(testImageBytes[i], image.Bytes[i]);
            }
        }

        [Theory(DisplayName = "Image removal"), MemberData(nameof(SupportedAudioFileNames))]
        public async Task ImageRemovalTest(string fileName)
        {
            var file = await GetTestMediaFileAsync(fileName);
            file = await CopyToTempFileAsync(file);

            var fileInfo = await Task.Run(() => TagManager.ReadFile(file));

            fileInfo.Tag.Image = null;
            await Task.Run(() => TagManager.WriteFile(file, fileInfo.Tag));

            fileInfo = await Task.Run(() => TagManager.ReadFile(file));
            Assert.Null(fileInfo.Tag.Image);
        }

        private async Task<IStorageFile> GetTestMediaFileAsync(string fileName)
        {
            var mediaFolderPath = Path.Combine(Package.Current.InstalledLocation.Path, "TestMedia");
            var mediaFolder = await StorageFolder.GetFolderFromPathAsync(mediaFolderPath);
            var file = await mediaFolder.GetFileAsync(fileName);
            return file;
        }

        private async Task<IStorageFile> CopyToTempFileAsync(IStorageFile input)
        {
            var folder = ApplicationData.Current.TemporaryFolder;
            var output = await input.CopyAsync(folder, "Temp-" + input.Name, NameCollisionOption.ReplaceExisting);
            return output;
        }
    }
}

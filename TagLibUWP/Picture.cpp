#include "Picture.h"

#include <memory>

using namespace std;

namespace TagLibUWP
{
	Picture::Picture()
	{
	}

	Picture^ Picture::FromPictureMape(const TagLib::PictureMap& pictureMap)
	{
		if (pictureMap.isEmpty())
			return nullptr;

		auto picture = GetPictureFromMapIfPresent(pictureMap, DefaultPictureType);
		if (picture == nullptr)
		{
			picture = GetPictureFromMapIfPresent(pictureMap, TagLib::Picture::Type::BackCover);
		}
		if (picture == nullptr)
		{
			picture = &(pictureMap.begin()->second.front());
		}

		auto pictureData = picture->data();
		auto dataPtr = reinterpret_cast<uint8*>(pictureData.data());
		auto output = ref new Picture();
		output->Data = ref new Platform::Array<uint8>(dataPtr, pictureData.size());
		output->MIMEType = ref new Platform::String(picture->mime().toCWString());
		return output;
	}

	TagLib::PictureMap Picture::ToPictureMap()
	{
		TagLib::PictureMap output;
		if (!Valid)
			return output;

		auto dataPtr = reinterpret_cast<char*>(Data->Data);
		TagLib::Picture picture(TagLib::ByteVector(dataPtr, Data->Length), DefaultPictureType, TagLib::String(MIMEType->Data()));

		output.insert(picture);
		return output;
	}

	const TagLib::Picture* Picture::GetPictureFromMapIfPresent(const TagLib::PictureMap& pictureMap, TagLib::Picture::Type pictureType)
	{
		if (pictureMap.contains(pictureType))
		{
			return &pictureMap[pictureType].front();
		}

		return nullptr;
	}

}


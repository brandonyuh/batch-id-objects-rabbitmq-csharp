using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.Dnn;
public class Detection
{
    public static String DrawBoxes(string imagePath)
    {
        Mat image = CvInvoke.Imread(imagePath);

        return imagePath;
    }
}

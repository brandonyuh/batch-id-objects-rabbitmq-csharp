using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.Dnn;

public class Detection
{
    public static String DrawBoxes(string imagePath)
    {
        // you can download better models here https://pjreddie.com/darknet/yolo/ 
        var net = Emgu.CV.Dnn.DnnInvoke.ReadNetFromDarknet(
            "./detection/yolov3-tiny.cfg",
            "./detection/yolov3-tiny.weights"
        );

        var classLabels = File.ReadAllLines("./detection/coco.names");

        net.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
        net.SetPreferableTarget(Emgu.CV.Dnn.Target.Cpu);

        Mat frame = CvInvoke.Imread(imagePath);

        VectorOfMat output = new();
        VectorOfRect boxes = new();
        VectorOfFloat scores = new();
        VectorOfInt indices = new();

        CvInvoke.Resize(frame, frame, new System.Drawing.Size(512, 512), 0.4, 0.4);

        var image = frame.ToImage<Bgr, byte>();

        var input = DnnInvoke.BlobFromImage(image, 1 / 255.0, swapRB: true);

        net.SetInput(input);
        net.Forward(output, net.UnconnectedOutLayersNames);

        for (int i = 0; i < output.Size; i++)
        {
            var mat = output[i];
            var data = (float[,])mat.GetData();

            for (int j = 0; j < data.GetLength(0); j++)
            {
                float[] row = Enumerable
                    .Range(0, data.GetLength(1))
                    .Select(x => data[j, x])
                    .ToArray();

                var rowScore = row.Skip(5).ToArray();
                var classId = rowScore.ToList().IndexOf(rowScore.Max());
                var confidence = rowScore[classId];

                //if the model thinks it's 80% sure or more that it's found an object
                if (confidence > 0.8f)
                {
                    //draw the box around the detected object
                    var centerX = (int)(row[0] * frame.Width);
                    var centerY = (int)(row[1] * frame.Height);
                    var boxWidth = (int)(row[2] * frame.Width);
                    var boxHeight = (int)(row[3] * frame.Height);

                    var x = (int)(centerX - (boxWidth / 2));
                    var y = (int)(centerY - (boxHeight / 2));

                    boxes.Push(
                        new System.Drawing.Rectangle[]
                        {
                            new System.Drawing.Rectangle(x, y, boxWidth, boxHeight)
                        }
                    );
                    indices.Push(new int[] { classId });
                    scores.Push(new float[] { confidence });
                }
            }
        }

        var bestDetectionIndices = DnnInvoke.NMSBoxes(boxes.ToArray(), scores.ToArray(), .8f, .8f);

        var frameOut = frame.ToImage<Bgr, byte>();

        for (int i = 0; i < bestDetectionIndices.Length; i++)
        {
            //convert the score to a percentage
            int displayScore = (int)(scores[i] * 100);
            int index = bestDetectionIndices[i];
            var box = boxes[index];

            //draw a green rectangle around the detected object
            CvInvoke.Rectangle(frameOut, box, new MCvScalar(0, 255, 0), 2);
            string displayText = classLabels[indices[index]]; //+ " " + displayScore + "%";

            //put the text 20 pixels above the detected object
            CvInvoke.PutText(
                frameOut,
                displayText,
                new System.Drawing.Point(box.X, box.Y - 20),
                Emgu.CV.CvEnum.FontFace.HersheyPlain,
                0.5,
                new MCvScalar(0, 0, 255),
                1
            );
        }

        //restore the image to its original size
        CvInvoke.Resize(frameOut, frameOut, new System.Drawing.Size(0, 0), 4, 4);
        string newPath = "out/" + imagePath;
        CvInvoke.Imwrite(newPath, frameOut);
        return newPath;
    }
}

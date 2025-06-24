using Microsoft.AspNetCore.Mvc;
using OpenCvSharp;
using testti.Models;
namespace testti.Controllers
{
    public class FaceRecognitionController : Controller
    {
        private readonly ApplicationDbcontext _context;
        private readonly IWebHostEnvironment _env;
        public FaceRecognitionController(ApplicationDbcontext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile imageFile, string name)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                var savePath = Path.Combine(_env.WebRootPath, "uploads", fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                var user = new Usercs
                {
                    Name = name,
                    image = $"/uploads/{fileName}"
                };

                _context.users.Add(user);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Compare");// dùng cam thì chuyển Compare thành Capture
        }
        [HttpGet]
        public IActionResult Compare()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Compare(IFormFile testImage)
        {
            if (testImage == null || testImage.Length == 0)
            {
                ViewBag.Message = "Bạn chưa chọn ảnh!";
                ViewBag.Success = false;
                return View();
            }

            // Lưu ảnh tạm
            var fileName = Guid.NewGuid() + Path.GetExtension(testImage.FileName);
            var tempPath = Path.Combine(_env.WebRootPath, "temp");
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);
            var testImagePath = Path.Combine(tempPath, fileName);

            using (var stream = new FileStream(testImagePath, FileMode.Create))
            {
                await testImage.CopyToAsync(stream);
            }

            // Đọc ảnh test
            var testImg = Cv2.ImRead(testImagePath);
            Cv2.CvtColor(testImg, testImg, ColorConversionCodes.BGR2GRAY);

            // Load ảnh đã đăng ký (giả sử lấy user đầu tiên)
            var user = _context.users.FirstOrDefault();
            if (user == null)
            {
                ViewBag.Message = "Chưa có người dùng nào đã đăng ký ảnh.";
                ViewBag.Success = false;
                return View();
            }

            var registeredPath = Path.Combine(_env.WebRootPath, user.image.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
            var dbImg = Cv2.ImRead(registeredPath);
            Cv2.CvtColor(dbImg, dbImg, ColorConversionCodes.BGR2GRAY);

            var recognizer = OpenCvSharp.Face.LBPHFaceRecognizer.Create();
            recognizer.Train(new[] { dbImg }, new[] { 1 });

            recognizer.Predict(testImg, out int label, out double confidence);

            if (label == 1 && confidence < 80)
            {
                ViewBag.Message = $"Ảnh trùng khớp với người dùng: {user.Name} (Độ tin cậy: {confidence:0.00})";
                ViewBag.Success = true;
            }
            else
            {
                ViewBag.Message = $"Không khớp với người dùng đã đăng ký. (Độ tin cậy: {confidence:0.00})";
                ViewBag.Success = false;
            }

            return View();
        }
        //[HttpGet]
        //public async Task<IActionResult> Capture()
        //{
        //    return View();
        //}
        //[HttpPost]
        //public async Task<IActionResult> Recognize(FaceImageInput model)
        //{
        //    var capturedPath = Path.Combine(_env.WebRootPath, "temp", "captured.png");
        //    var imageData = Convert.FromBase64String(model.Base64Image.Replace("data:image/png;base64,", ""));
        //    System.IO.File.WriteAllBytes(capturedPath, imageData);

        //    var capturedImg = Cv2.ImRead(capturedPath);
        //    Cv2.CvtColor(capturedImg, capturedImg, ColorConversionCodes.BGR2GRAY);

        //    var users = _context.users.ToList();

        //    foreach (var user in users)
        //    {
        //        var fullPath = Path.Combine(_env.WebRootPath, user.image.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
        //        var dbImg = Cv2.ImRead(fullPath);
        //        Cv2.CvtColor(dbImg, dbImg, ColorConversionCodes.BGR2GRAY);

        //        var recognizer = OpenCvSharp.Face.LBPHFaceRecognizer.Create();
        //        recognizer.Train(new[] { dbImg }, new[] { 1 });

        //        int predictedLabel;
        //        double confidence;

        //        recognizer.Predict(capturedImg, out predictedLabel, out confidence);

        //        if (predictedLabel == 1 && confidence < 80)
        //        {
        //            var attendance = new Attendance
        //            {
        //                UserId = user.Id,
        //                time = DateTime.Now,
        //                status = "Success"
        //            };

        //            _context.Attendances.Add(attendance);
        //            await _context.SaveChangesAsync();

        //            return Json(new { success = true, message = $"Xin chào {user.Name}. Điểm danh thành công!" });
        //        }
        //    }

        //    return Json(new { success = false, message = "Không khớp khuôn mặt nào trong hệ thống!" });
        //}
        public class FaceImageInput
        {
            public string Base64Image { get; set; } = string.Empty;
        }


    }
}

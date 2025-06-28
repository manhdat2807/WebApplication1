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
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile imageFile, string name)
        {
            if (imageFile == null || imageFile.Length == 0 || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Vui lòng nhập tên và chọn ảnh!";
                return RedirectToAction("Index");
            }

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
            var fullPath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
                await imageFile.CopyToAsync(stream);

            _context.users.Add(new Usercs
            {
                Name = name,
                image = "/uploads/" + fileName
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("Capture");
        }

        [HttpGet]
        public IActionResult Compare() => View();

        [HttpPost]
        public async Task<IActionResult> Compare(IFormFile testImage)
        {
            if (testImage == null || testImage.Length == 0)
            {
                ViewBag.Message = "Bạn chưa chọn ảnh!";
                ViewBag.Success = false;
                return View();
            }

            var tempPath = Path.Combine(_env.WebRootPath, "temp");
            Directory.CreateDirectory(tempPath);
            var fileName = Guid.NewGuid() + Path.GetExtension(testImage.FileName);
            var fullPath = Path.Combine(tempPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
                await testImage.CopyToAsync(stream);

            var testMat = Cv2.ImRead(fullPath, ImreadModes.Grayscale);
            if (testMat.Empty())
            {
                ViewBag.Message = "Không thể đọc ảnh kiểm tra.";
                ViewBag.Success = false;
                return View();
            }
            Cv2.EqualizeHist(testMat, testMat);

            var user = _context.users.FirstOrDefault();
            if (user == null)
            {
                ViewBag.Message = "Chưa có người dùng nào đã đăng ký ảnh.";
                ViewBag.Success = false;
                return View();
            }

            var regPath = Path.Combine(_env.WebRootPath, "uploads", Path.GetFileName(user.image));
            var regMat = Cv2.ImRead(regPath, ImreadModes.Grayscale);
            if (regMat.Empty())
            {
                ViewBag.Message = "Không thể đọc ảnh đăng ký.";
                ViewBag.Success = false;
                return View();
            }
            Cv2.EqualizeHist(regMat, regMat);

            var recognizer = OpenCvSharp.Face.LBPHFaceRecognizer.Create();
            recognizer.Train(new[] { regMat }, new[] { 1 });
            recognizer.Predict(testMat, out int label, out double confidence);

            if (label == 1 && confidence < 80)
            {
                ViewBag.Message = $"Khớp với: {user.Name} (Độ tin cậy: {confidence:0.00})";
                ViewBag.Success = true;
            }
            else
            {
                ViewBag.Message = $"Không khớp! (Độ tin cậy: {confidence:0.00})";
                ViewBag.Success = false;
            }

            return View();
        }

        [HttpGet]
        public IActionResult Capture()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Recognize([FromBody] FaceImageInput model)
        {
            if (string.IsNullOrWhiteSpace(model?.Base64Image))
                return BadRequest("Ảnh gửi lên rỗng.");

            try
            {
                var clean64 = model.Base64Image
                    .Replace("data:image/png;base64,", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("data:image/jpeg;base64,", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(" ", "+");

                byte[] imageBytes = Convert.FromBase64String(clean64);
                var captured = Cv2.ImDecode(imageBytes, ImreadModes.Grayscale);
                if (captured.Empty())
                    return BadRequest("Không thể đọc ảnh từ base64.");

                Cv2.EqualizeHist(captured, captured);

                var faces = new List<Mat>();
                var labels = new List<int>();

                foreach (var user in _context.users)
                {
                    var imagePath = Path.Combine(_env.WebRootPath, user.image.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (!System.IO.File.Exists(imagePath))
                        continue;

                    var userImg = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
                    if (userImg.Empty()) continue;

                    Cv2.EqualizeHist(userImg, userImg);

                    faces.Add(userImg);
                    labels.Add(user.Id);
                }

                if (faces.Count == 0)
                    return BadRequest("Không có ảnh nào để nhận diện.");

                var recognizer = OpenCvSharp.Face.LBPHFaceRecognizer.Create();
                recognizer.Train(faces, labels);

                recognizer.Predict(captured, out int predictedId, out double confidence);

                var matchedUser = _context.users.FirstOrDefault(u => u.Id == predictedId);
                if (matchedUser != null && confidence < 80)
                {
                    _context.Attendances.Add(new Attendance
                    {
                        UserId = matchedUser.Id,
                        time = DateTime.UtcNow,
                        status = "Success"
                    });
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Xin chào {matchedUser.Name}! Điểm danh thành công. (Độ tin cậy: {confidence:0.00})"
                    });
                }

                return Json(new { success = false, message = "❌ Không khớp khuôn mặt nào." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        public class FaceImageInput
        {
            public string Base64Image { get; set; } = string.Empty;
        }
    }
}
<!-- 代码已包含 CSS：使用 TailwindCSS , 安装 TailwindCSS 后方可看到布局样式效果 -->
<template>
  <div class="login-container">
    <!-- 左侧品牌区 -->
    <div class="brand-area">
      <div class="background-wrapper">
        <img
          :src="'https://ai-public.mastergo.com/ai/img_res/7041b951edbc8d5519bd51be79e1a7b2.jpg'"
          alt="Background"
        />
      </div>
      <div class="brand-content">
        <div class="logo-wrapper">
          <img
            :src="'https://ai-public.mastergo.com/ai/img_res/c87375e779184d55f4a54c5b936ca619.jpg'"
            alt="Logo"
          />
        </div>
        <div class="illustration-wrapper">
          <img
            :src="'https://ai-public.mastergo.com/ai/img_res/ff722f2fc7328239815881a15bb29d62.jpg'"
            alt="Illustration"
          />
        </div>
        <div class="brand-text">
          <h2>智能制造测试</h2>
          <p>FA 测试平台，助力制造业智能化升级</p>
        </div>
      </div>
    </div>
    <!-- 右侧登录区 -->
    <div class="login-area">
      <div class="login-box">
        <div class="login-header">
          <h1>Kingsemi</h1>
          <p>欢迎使用 FA 测试平台</p>
        </div>
        <form @submit.prevent="handleLogin">
          <div class="form-item">
            <input
              v-model="form.name"
              type="text"
              placeholder="请输入用户名"
              :class="{ error: errors.name }"
            />
            <i class="fas fa-user"></i>
            <span v-if="errors.name" class="error-message">{{
              errors.name
            }}</span>
          </div>
          <div class="form-item">
            <input
              v-model="form.password"
              :type="showPassword ? 'text' : 'password'"
              placeholder="请输入密码"
              :class="{ error: errors.password }"
            />
            <i class="fas fa-lock"></i>
            <i
              :class="showPassword ? 'fas fa-eye' : 'fas fa-eye-slash'"
              class="toggle-password"
              @click="showPassword = !showPassword"
            ></i>
            <span v-if="errors.password" class="error-message">{{
              errors.password
            }}</span>
          </div>
          <div class="form-options">
            <label class="remember-me">
              <input type="checkbox" v-model="rememberMe" />
              <span>记住密码</span>
            </label>
          </div>
          <button
            type="submit"
            class="submit-button !rounded-button"
            :disabled="isLoading"
          >
            <i v-if="isLoading" class="fas fa-circle-notch fa-spin"></i>
            {{ isLoading ? "登录中..." : "登录" }}
          </button>
          <div class="copyright">© 上海芯源微 2025</div>
        </form>
      </div>
    </div>
  </div>
</template>
  <script>
  import { login } from "@/api/user"
export default {
  name: "FaLoginPage",
  data() {
    return {
      form: {
        name: "",
        password: "",
      },
      errors: {
        name: "",
        password: "",
      },
      isLoading: false,
      showPassword: false,
      rememberMe: false,
    };
  },
  methods: {
    validateForm() {
      let isValid = true;
      this.errors = {
        name: "",
        password: "",
      };
      if (!this.form.name) {
        this.errors.name = "请输入用户名";
        isValid = false;
      }
      if (!this.form.password) {
        this.errors.password = "请输入密码";
        isValid = false;
      } else if (this.form.password.length < 6) {
        this.errors.password = "密码长度不能少于6位";
        isValid = false;
      }
      return isValid;
    },
    async handleLogin() {
      if (!this.validateForm()) {
        return;
      }
      this.isLoading = true;
      try {
        return login(this.form).then(res => {
          this.$router.push({path: "/Dashboard"})
        })
      } catch (error) {
        console.error("Login failed:", error);
      } finally {
        this.isLoading = false;
      }
    },
  },
};
</script>
  <style scoped>
.login-container {
  display: flex;
  height: 100vh;
}
.brand-area {
  display: none;
  position: relative;
  overflow: hidden;
  width: 60%;
}
@media (min-width: 1024px) {
  .brand-area {
    display: flex;
  }
}
.background-wrapper {
  position: absolute;
  inset: 0;
}
.background-wrapper img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
}
.brand-content {
  position: relative;
  z-index: 10;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 48px;
  color: white;
}
.logo-wrapper {
  text-align: center;
}
.logo-wrapper img {
  width: 240px;
  margin-bottom: 32px;
}
.illustration-wrapper {
  text-align: center;
}
.illustration-wrapper img {
  width: 100%;
  max-width: 448px;
  margin-bottom: 32px;
}
.brand-text {
  text-align: center;
}
.brand-text h2 {
  font-size: 24px;
  font-weight: 600;
  margin-bottom: 16px;
}
.brand-text p {
  color: #cbd5e1;
}
.login-area {
  width: 40%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
  position: relative;
  overflow: hidden;
}
.login-area::before {
  content: "";
  position: absolute;
  width: 1000px;
  height: 1000px;
  background: radial-gradient(
    circle,
    rgba(59, 130, 246, 0.05) 0%,
    transparent 70%
  );
  top: -400px;
  right: -400px;
  border-radius: 50%;
}
.login-area::after {
  content: "";
  position: absolute;
  width: 800px;
  height: 800px;
  background: radial-gradient(
    circle,
    rgba(59, 130, 246, 0.05) 0%,
    transparent 70%
  );
  bottom: -300px;
  left: -300px;
  border-radius: 50%;
}
@media (min-width: 1024px) {
  .login-area {
    padding: 0 64px;
  }
}
.login-box {
  width: 100%;
  max-width: 448px;
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(10px);
  border-radius: 20px;
  padding: 64px 32px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.05);
  position: relative;
  z-index: 1;
}
.login-header {
  text-align: center;
  margin-bottom: 48px;
}
@import url("https://fonts.googleapis.com/css2?family=Orbitron:wght@700&display=swap");
.login-header h1 {
  font-size: 32px;
  font-weight: 700;
  background: linear-gradient(135deg, #1e40af 0%, #3b82f6 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  margin-bottom: 12px;
  font-family: "Orbitron", sans-serif;
  letter-spacing: 2px;
}
.login-header p {
  color: #4b5563;
}
form {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.form-item {
  position: relative;
}
.form-item input {
  width: 100%;
  padding: 12px 40px 12px 40px;
  border: 2px solid #e2e8f0;
  border-radius: 12px;
  transition: all 0.3s;
  background: rgba(255, 255, 255, 0.9);
  font-size: 15px;
  box-sizing: border-box;
}
.form-item input:focus {
  outline: none;
  border-color: transparent;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.5);
}
.form-item input.error {
  border-color: #ef4444;
}
.form-item i {
  position: absolute;
  left: 16px;
  top: 50%;
  transform: translateY(-50%);
  color: #64748b;
  font-size: 18px;
}
.form-item .toggle-password {
  left: auto;
  right: 16px;
  cursor: pointer;
}
.error-message {
  display: block;
  color: #ef4444;
  font-size: 14px;
  margin-top: 4px;
}
.form-options {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.remember-me {
  display: flex;
  align-items: center;
  cursor: pointer;
}
.remember-me input[type="checkbox"] {
  width: 18px;
  height: 18px;
  margin-right: 8px;
  cursor: pointer;
  border-radius: 4px;
  border: 2px solid #cbd5e1;
  accent-color: #3b82f6;
}
.remember-me span {
  color: #374151;
}
.forgot-password {
  color: #2563eb;
  font-size: 14px;
  text-decoration: none;
}
.forgot-password:hover {
  color: #1d4ed8;
}
.submit-button {
  width: 100%;
  padding: 12px 16px;
  background: linear-gradient(135deg, #1e40af 0%, #3b82f6 100%);
  color: white;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.3s;
  font-size: 16px;
  font-weight: 600;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.2);
  box-sizing: border-box;
}
.submit-button:hover {
  background: linear-gradient(135deg, #1e3a8a 0%, #2563eb 100%);
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(37, 99, 235, 0.3);
}
.submit-button:hover {
  background-color: #1d4ed8;
}
.submit-button:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}
.terms {
  margin-top: 32px;
  text-align: center;
  font-size: 14px;
  color: #4b5563;
}
.terms a {
  color: #2563eb;
  text-decoration: none;
}
.terms a:hover {
  color: #1d4ed8;
}
.copyright {
  text-align: center;
  color: #64748b;
  font-size: 14px;
  margin-top: 16px;
}
/* 移除输入框number类型的箭头 */
input[type="number"]::-webkit-inner-spin-button,
input[type="number"]::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}
input[type="number"] {
  -moz-appearance: textfield;
}
</style>
  